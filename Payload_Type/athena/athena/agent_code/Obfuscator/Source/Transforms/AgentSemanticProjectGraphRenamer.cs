using System.Diagnostics;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace Obfuscator.Source.Transforms;

public static class AgentSemanticProjectGraphRenamer
{
    private static readonly object RegistrationLock = new();
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedProperties =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Configuration"] = new(StringComparer.Ordinal) { "Debug", "Release" },
            ["HandlerOS"] = new(StringComparer.Ordinal) { "windows", "linux", "redhat", "macos" },
            ["CryptoProvider"] = new(StringComparer.Ordinal) { "Aes", "None" },
        };

    public static AgentSemanticProjectGraphRenameResult Transform(
        string workspaceRoot,
        string rootProjectPath,
        IReadOnlyDictionary<string, SyntaxTree> postTransformTrees,
        IReadOnlyDictionary<string, string> globalProperties,
        Guid payloadUuid,
        int seed,
        CancellationToken cancellationToken = default) =>
        TransformAsync(workspaceRoot, rootProjectPath, postTransformTrees, globalProperties,
            payloadUuid, seed, cancellationToken).GetAwaiter().GetResult();

    private static async Task<AgentSemanticProjectGraphRenameResult> TransformAsync(
        string workspaceRoot,
        string rootProjectPath,
        IReadOnlyDictionary<string, SyntaxTree> postTransformTrees,
        IReadOnlyDictionary<string, string> globalProperties,
        Guid payloadUuid,
        int seed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(postTransformTrees);
        var root = PathIdentity.Normalize(workspaceRoot);
        var projectPath = PathIdentity.Normalize(rootProjectPath);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Project workspace does not exist: {root}");
        if (!PathIdentity.IsWithin(projectPath, root) || !File.Exists(projectPath)
            || !string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The root project must be an existing .csproj inside the workspace.",
                nameof(rootProjectPath));
        var properties = ValidateProperties(globalProperties);
        await RestoreProjectGraph(projectPath, root, properties, cancellationToken)
            .ConfigureAwait(false);
        EnsureMSBuildRegistered();

        using var workspace = MSBuildWorkspace.Create(properties);
        var failures = new List<string>();
        workspace.WorkspaceFailed += (_, args) =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                failures.Add(args.Diagnostic.Message);
        };
        var rootProject = await workspace.OpenProjectAsync(
            projectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (failures.Count != 0)
            throw new AgentSemanticRenameException(
                "MSBuild could not load the selected payload project graph."
                + Environment.NewLine + string.Join(Environment.NewLine, failures));

        var solution = rootProject.Solution;
        var graphProjectIds = solution.GetProjectDependencyGraph()
            .GetProjectsThatThisProjectTransitivelyDependsOn(rootProject.Id)
            .Append(rootProject.Id)
            .ToHashSet();
        foreach (var (path, tree) in postTransformTrees)
        {
            var normalizedPath = PathIdentity.Normalize(path);
            if (!PathIdentity.IsWithin(normalizedPath, root))
                throw new ArgumentException("A post-transform tree is outside the workspace.",
                    nameof(postTransformTrees));
            foreach (var documentId in solution.GetDocumentIdsWithFilePath(normalizedPath)
                .Where(id => graphProjectIds.Contains(id.ProjectId)))
            {
                solution = solution.WithDocumentSyntaxRoot(
                    documentId, await tree.GetRootAsync(cancellationToken).ConfigureAwait(false),
                    PreservationMode.PreserveIdentity);
            }
        }

        var orderedProjectIds = solution.GetProjectDependencyGraph()
            .GetTopologicallySortedProjects(cancellationToken)
            .Where(graphProjectIds.Contains)
            .ToArray();
        var compilationsByProjectId = new Dictionary<ProjectId, CSharpCompilation>();
        var projectPaths = new List<string>(orderedProjectIds.Length);
        foreach (var projectId in orderedProjectIds)
        {
            var project = solution.GetProject(projectId)
                ?? throw new AgentSemanticRenameException("A selected graph project disappeared while loading.");
            if (await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
                is not CSharpCompilation compilation)
                throw new AgentSemanticRenameException($"Project is not a C# compilation: {project.FilePath}");
            compilationsByProjectId.Add(projectId, compilation);
            projectPaths.Add(PathIdentity.Normalize(project.FilePath!));
        }

        var compilations = RebindProjectDependencies(
            solution, orderedProjectIds, compilationsByProjectId);
        var renamed = AgentSemanticRenamer.TransformProjectSet(
            compilations, payloadUuid, seed, cancellationToken);
        var renamedByProjectId = orderedProjectIds
            .Select((projectId, index) => (projectId, compilation: renamed.Compilations[index]))
            .ToDictionary(pair => pair.projectId, pair => pair.compilation);
        var reboundRenamed = RebindProjectDependencies(
            solution, orderedProjectIds, renamedByProjectId);
        foreach (var compilation in reboundRenamed)
        {
            var errors = compilation.GetDiagnostics(cancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
            if (errors.Length != 0)
                throw new AgentSemanticRenameException(
                    "Project-ID dependency rebinding produced an invalid compilation."
                    + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
        var documents = new Dictionary<string, string>(PathIdentity.Comparer);
        foreach (var compilation in reboundRenamed)
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (string.IsNullOrEmpty(tree.FilePath))
                continue;
            var path = PathIdentity.Normalize(tree.FilePath);
            if (!PathIdentity.IsWithin(path, root)
                || path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "obj" or "bin")
                || !File.Exists(path))
                continue;
            documents[path] = tree.GetRoot(cancellationToken).ToFullString();
        }

        return new AgentSemanticProjectGraphRenameResult(
            projectPaths, documents, reboundRenamed, renamed.Plan);
    }

    private static CSharpCompilation[] RebindProjectDependencies(
        Solution solution,
        IReadOnlyList<ProjectId> orderedProjectIds,
        IReadOnlyDictionary<ProjectId, CSharpCompilation> sourceCompilations)
    {
        var selectedProjectIds = orderedProjectIds.ToHashSet();
        var rebound = new Dictionary<ProjectId, CSharpCompilation>();
        var visiting = new HashSet<ProjectId>();

        CSharpCompilation Rebind(ProjectId projectId)
        {
            if (rebound.TryGetValue(projectId, out var existing))
                return existing;
            if (!visiting.Add(projectId))
                throw new AgentSemanticRenameException("The selected project graph contains a dependency cycle.");

            var project = solution.GetProject(projectId)
                ?? throw new AgentSemanticRenameException("A selected graph project disappeared while rebinding.");
            var compilation = sourceCompilations[projectId];
            var compilationReferences = compilation.References
                .OfType<CompilationReference>()
                .Cast<MetadataReference>()
                .ToArray();
            if (compilationReferences.Length != 0)
                compilation = compilation.RemoveReferences(compilationReferences);

            foreach (var projectReference in project.ProjectReferences
                .Where(reference => selectedProjectIds.Contains(reference.ProjectId)))
            {
                var dependency = Rebind(projectReference.ProjectId);
                compilation = compilation.AddReferences(dependency.ToMetadataReference(
                    projectReference.Aliases, projectReference.EmbedInteropTypes));
            }

            visiting.Remove(projectId);
            rebound.Add(projectId, compilation);
            return compilation;
        }

        return orderedProjectIds.Select(Rebind).ToArray();
    }

    private static async Task RestoreProjectGraph(
        string projectPath,
        string workspaceRoot,
        IReadOnlyDictionary<string, string> properties,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("restore");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--nologo");
        foreach (var (name, value) in properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            startInfo.ArgumentList.Add($"-p:{name}={value}");

        using var process = Process.Start(startInfo)
            ?? throw new AgentSemanticRenameException("Could not start dotnet restore for the payload graph.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new AgentSemanticRenameException(
                $"dotnet restore failed for the selected payload graph (exit {process.ExitCode})."
                + Environment.NewLine + output + Environment.NewLine + error);
    }

    private static Dictionary<string, string> ValidateProperties(
        IReadOnlyDictionary<string, string> globalProperties)
    {
        ArgumentNullException.ThrowIfNull(globalProperties);
        if (globalProperties.Count != AllowedProperties.Count)
            throw new ArgumentException(
                "Exactly Configuration, HandlerOS, and CryptoProvider are required.",
                nameof(globalProperties));
        var validated = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in globalProperties)
        {
            if (!AllowedProperties.TryGetValue(name, out var allowed) || !allowed.Contains(value))
                throw new ArgumentException($"Invalid project property '{name}' or value '{value}'.",
                    nameof(globalProperties));
            validated.Add(name, value);
        }
        return validated;
    }

    private static void EnsureMSBuildRegistered()
    {
        lock (RegistrationLock)
        {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
        }
    }
}

public sealed record AgentSemanticProjectGraphRenameResult(
    IReadOnlyList<string> ProjectPaths,
    IReadOnlyDictionary<string, string> Documents,
    IReadOnlyList<CSharpCompilation> Compilations,
    AgentSemanticRenamePlan Plan);
