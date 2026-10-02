using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Obfuscator.Config;
using Obfuscator.IL;
using Obfuscator.Source.Transforms;

namespace Obfuscator.Source;

public sealed class SourceRewriter
{
    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    public void Rewrite(ObfuscationConfig config)
    {
        if (config.Uuid is not null)
            _ = UuidRenameMap.NormalizeUuid(config.Uuid);

        var inputDir = PathIdentity.Normalize(config.InputPath);
        var outputDir = PathIdentity.Normalize(config.OutputPath);

        if (!PathIdentity.Comparer.Equals(inputDir, outputDir))
            CopyDirectory(inputDir, outputDir);

        var (decryptorNs, decryptorClass, decryptorMethod,
             callerNs, callerClass, callerMethod) = GenerateHelperNames(config.Seed);

        var projectDirs = Directory.EnumerateFiles(
                outputDir, "*.csproj", SearchOption.AllDirectories)
            .Select(f => PathIdentity.Normalize(Path.GetDirectoryName(f) ?? outputDir))
            .Distinct(PathIdentity.Comparer)
            .ToList();

        var generatedFiles = new HashSet<string>(PathIdentity.Comparer);
        var generatedOriginals = new Dictionary<string, byte[]?>(PathIdentity.Comparer);
        var outputDocuments = new Dictionary<string, string>(PathIdentity.Comparer);
        UuidRenameMap? uuidMap = null;

        try
        {
            StageRuntimeHelpers(
                projectDirs,
                decryptorNs, decryptorClass, decryptorMethod,
                callerNs, callerClass, callerMethod,
                generatedFiles, generatedOriginals);

            var contractsDir = Path.Combine(outputDir, "Agent.Models");
            uuidMap = DeriveUuidRenameMap(config.Uuid, contractsDir);

            var (sourceFiles, trees, owningProjects, semanticSupportPaths) =
                LoadSourceWorkspace(outputDir, contractsDir, projectDirs, generatedFiles);

            // Earlier transforms replace trees. Keep each owning project's source
            // group current so semantic binding sees siblings without crossing
            // project boundaries.
            foreach (var file in sourceFiles)
            {
                trees[file] = ApplyNonUuidTransforms(
                    trees[file],
                    config.Seed,
                    GetSemanticContextTrees(file, trees, owningProjects, semanticSupportPaths),
                    decryptorNs, decryptorClass, decryptorMethod,
                    callerNs, callerClass, callerMethod);
            }

            if (uuidMap is not null)
                ApplyUuidRenames(sourceFiles, trees, owningProjects, semanticSupportPaths, uuidMap);

            outputDocuments = trees.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.GetRoot().ToFullString(),
                PathIdentity.Comparer);
            foreach (var generatedFile in generatedFiles)
                outputDocuments[generatedFile] = File.ReadAllText(generatedFile);

            if (config.EnableBroadSemanticRename)
                ApplyBroadSemanticRename(config, outputDir, trees, outputDocuments);
        }
        finally
        {
            RestoreGeneratedFiles(generatedOriginals);
        }

        FileRewriteTransaction.Commit(outputDocuments.Select(pair =>
            new FileRewrite(File.Exists(pair.Key) ? pair.Key : null, pair.Key,
                Encoding.UTF8.GetBytes(pair.Value))));

        if (config.MapPath is not null)
        {
            WriteDeobfuscationMap(
                config,
                decryptorNs, decryptorClass, decryptorMethod,
                callerNs, callerClass, callerMethod,
                uuidMap);
        }
    }

    private static void StageRuntimeHelpers(
        IReadOnlyList<string> projectDirs,
        string decryptorNs, string decryptorClass, string decryptorMethod,
        string callerNs, string callerClass, string callerMethod,
        HashSet<string> generatedFiles,
        Dictionary<string, byte[]?> generatedOriginals)
    {
        var decryptorReplacements = new Dictionary<string, string>
        {
            ["__OBFS_NS__"] = decryptorNs,
            ["__OBFS_CLASS__"] = decryptorClass,
            ["__OBFS_METHOD__"] = decryptorMethod,
        };
        var callerReplacements = new Dictionary<string, string>
        {
            ["__OBFS_NS__"] = callerNs,
            ["__OBFS_CALLER_CLASS__"] = callerClass,
            ["__OBFS_INVOKE_METHOD__"] = callerMethod,
        };

        foreach (var projDir in projectDirs)
        {
            StageSingleHelper(
                Path.Combine(projDir, "_generated_decryptor.cs"),
                "StringDecryptor.cs",
                decryptorReplacements,
                generatedFiles,
                generatedOriginals);
            StageSingleHelper(
                Path.Combine(projDir, "_generated_caller.cs"),
                "IndirectCaller.cs",
                callerReplacements,
                generatedFiles,
                generatedOriginals);
        }
    }

    private static void StageSingleHelper(
        string outputPath,
        string resourceName,
        Dictionary<string, string> replacements,
        HashSet<string> generatedFiles,
        Dictionary<string, byte[]?> generatedOriginals)
    {
        generatedOriginals[outputPath] = File.Exists(outputPath)
            ? File.ReadAllBytes(outputPath)
            : null;
        InjectRuntimeHelper(resourceName, outputPath, replacements);
        generatedFiles.Add(outputPath);
    }

    private static UuidRenameMap? DeriveUuidRenameMap(string? uuid, string contractsDir)
    {
        if (uuid is null)
            return null;
        var contractNames = Directory.Exists(contractsDir)
            ? ContractScanner.Scan(contractsDir)
            : new ContractNames([], [], [], [], []);
        return UuidRenameMap.Derive(uuid, contractNames);
    }

    private static (
        string[] SourceFiles,
        Dictionary<string, SyntaxTree> Trees,
        Dictionary<string, string> OwningProjects,
        HashSet<string> SemanticSupportPaths) LoadSourceWorkspace(
        string outputDir,
        string contractsDir,
        IReadOnlyList<string> projectDirs,
        HashSet<string> generatedFiles)
    {
        var excludedPrefixes = new[]
        {
            Path.Combine(outputDir, "Tests"),
            Path.Combine(outputDir, "Obfuscator"),
        };

        var sourceFiles = Directory.EnumerateFiles(
                outputDir, "*.cs", SearchOption.AllDirectories)
            .Select(PathIdentity.Normalize)
            .Where(file => !generatedFiles.Contains(file))
            .Where(file => !excludedPrefixes.Any(p => PathIdentity.IsWithin(file, p)))
            .ToArray();
        var trees = sourceFiles.ToDictionary(
            path => path,
            path => (SyntaxTree)CSharpSyntaxTree.ParseText(
                File.ReadAllText(path), path: path),
            PathIdentity.Comparer);
        var owningProjects = sourceFiles.ToDictionary(
            path => path,
            path => FindOwningProject(path, projectDirs, outputDir),
            PathIdentity.Comparer);
        var agentModelsProject = projectDirs.FirstOrDefault(project =>
            PathIdentity.Comparer.Equals(project, PathIdentity.Normalize(contractsDir)));
        var semanticSupportPaths = new HashSet<string>(
            owningProjects
                .Where(pair => agentModelsProject is not null
                    && PathIdentity.Comparer.Equals(pair.Value, agentModelsProject))
                .Select(pair => pair.Key),
            PathIdentity.Comparer);

        return (sourceFiles, trees, owningProjects, semanticSupportPaths);
    }

    private static void ApplyUuidRenames(
        IReadOnlyList<string> sourceFiles,
        Dictionary<string, SyntaxTree> trees,
        IReadOnlyDictionary<string, string> owningProjects,
        HashSet<string> semanticSupportPaths,
        UuidRenameMap uuidMap)
    {
        // Resolve every UUID rename against one immutable semantic snapshot,
        // then replace all trees together. Renaming a declaration must not
        // make references in later files unresolvable.
        var rewritten = new Dictionary<string, SyntaxTree>(PathIdentity.Comparer);
        foreach (var file in sourceFiles)
        {
            var tree = trees[file];
            var semanticModel = CreateSemanticModel(
                tree,
                GetSemanticContextTrees(file, trees, owningProjects, semanticSupportPaths));
            rewritten[file] = new UuidRenameTransform(uuidMap).Rewrite(tree, semanticModel);
        }
        foreach (var (file, tree) in rewritten)
            trees[file] = tree;
    }

    private static void ApplyBroadSemanticRename(
        ObfuscationConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SyntaxTree> trees,
        Dictionary<string, string> outputDocuments)
    {
        if (config.ProjectRoot is null || config.Configuration is null
            || config.HandlerOS is null || config.CryptoProvider is null
            || config.Uuid is null)
        {
            throw new ArgumentException(
                "Broad semantic renaming requires project root, Configuration, HandlerOS, CryptoProvider, and UUID.");
        }

        var projectRoot = Path.IsPathRooted(config.ProjectRoot)
            ? config.ProjectRoot
            : Path.Combine(outputDir, config.ProjectRoot);
        var graphResult = AgentSemanticProjectGraphRenamer.Transform(
            outputDir,
            projectRoot,
            trees,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Configuration"] = config.Configuration,
                ["HandlerOS"] = config.HandlerOS,
                ["CryptoProvider"] = config.CryptoProvider,
            },
            Guid.Parse(config.Uuid),
            config.Seed);
        foreach (var (path, content) in graphResult.Documents)
            outputDocuments[path] = content;
    }

    private static IReadOnlyList<SyntaxTree> GetSemanticSupportTrees(
        IReadOnlyDictionary<string, SyntaxTree> trees,
        HashSet<string> semanticSupportPaths)
    {
        return semanticSupportPaths
            .Where(trees.ContainsKey)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => trees[path])
            .ToArray();
    }

    private static string FindOwningProject(
        string file,
        IReadOnlyList<string> projectDirectories,
        string fallbackDirectory)
    {
        return projectDirectories
            .Where(directory => PathIdentity.IsWithin(file, directory))
            .OrderByDescending(directory => directory.Length)
            .ThenBy(directory => directory, PathIdentity.Comparer)
            .FirstOrDefault() ?? fallbackDirectory;
    }

    private static IReadOnlyList<SyntaxTree> GetSemanticContextTrees(
        string file,
        IReadOnlyDictionary<string, SyntaxTree> trees,
        IReadOnlyDictionary<string, string> owningProjects,
        HashSet<string> semanticSupportPaths)
    {
        var owner = owningProjects[file];
        var projectTrees = owningProjects
            .Where(pair => PathIdentity.Comparer.Equals(pair.Value, owner))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => trees[pair.Key])
            .ToArray();
        var projectDeclarations = GetDeclaredMetadataNames(projectTrees);
        var externalSemanticSupport = GetSemanticSupportTrees(
                trees, semanticSupportPaths)
            .Where(contract => projectTrees.All(
                project => !HasSameFilePath(project, contract)))
            .ToArray();
        externalSemanticSupport = FilterCollidingDeclarations(
            externalSemanticSupport, projectDeclarations);
        return projectTrees.Concat(externalSemanticSupport).ToArray();
    }

    private static SyntaxTree[] FilterCollidingDeclarations(
        IReadOnlyList<SyntaxTree> supportTrees,
        HashSet<string> projectDeclarations)
    {
        if (supportTrees.Count == 0 || projectDeclarations.Count == 0)
            return supportTrees.ToArray();

        var compilation = CreateLibraryCompilation(
            "SourceRewriteSemanticSupport", supportTrees);
        return supportTrees
            .Select(tree => RemoveCollidingDeclarations(
                tree,
                compilation.GetSemanticModel(tree, ignoreAccessibility: true),
                projectDeclarations))
            .ToArray();
    }

    private static SyntaxTree RemoveCollidingDeclarations(
        SyntaxTree supportTree,
        SemanticModel model,
        HashSet<string> projectDeclarations)
    {
        var colliding = supportTree.GetRoot().DescendantNodes()
            .Where(node => TryGetDeclaredMetadataName(model, node, out var name)
                && projectDeclarations.Contains(name))
            .ToHashSet();
        var topLevelCollisions = colliding
            .Where(declaration => !declaration.Ancestors().Any(colliding.Contains))
            .ToArray();
        return topLevelCollisions.Length == 0
            ? supportTree
            : supportTree.WithRootAndOptions(
                supportTree.GetRoot().RemoveNodes(
                    topLevelCollisions,
                    SyntaxRemoveOptions.KeepExteriorTrivia)!,
                supportTree.Options);
    }

    private static HashSet<string> GetDeclaredMetadataNames(
        IReadOnlyList<SyntaxTree> syntaxTrees)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (syntaxTrees.Count == 0)
            return result;
        var compilation = CreateLibraryCompilation(
            "SourceRewriteDeclarations", syntaxTrees);
        foreach (var syntaxTree in syntaxTrees)
        {
            var model = compilation.GetSemanticModel(
                syntaxTree, ignoreAccessibility: true);
            foreach (var node in syntaxTree.GetRoot().DescendantNodes())
            {
                if (TryGetDeclaredMetadataName(model, node, out var name))
                    result.Add(name);
            }
        }
        return result;
    }

    private static bool TryGetDeclaredMetadataName(
        SemanticModel model, SyntaxNode node, out string metadataName)
    {
        if (node is (BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            && model.GetDeclaredSymbol(node) is INamedTypeSymbol
            { TypeKind: not TypeKind.Error } type)
        {
            metadataName = ContractScanner.GetMetadataName(type);
            return true;
        }
        metadataName = string.Empty;
        return false;
    }

    private static SyntaxTree ApplyNonUuidTransforms(
        SyntaxTree tree,
        int seed,
        IReadOnlyList<SyntaxTree> semanticContextTrees,
        string decryptorNs, string decryptorClass, string decryptorMethod,
        string callerNs, string callerClass, string callerMethod)
    {
        var strTransform = new StringEncryptionTransform(
            decryptorClass, decryptorMethod, decryptorNs, seed);
        var semanticModel = CreateSemanticModel(tree, semanticContextTrees);
        tree = strTransform.MarkSemanticExemptions(tree, semanticModel);
        semanticContextTrees = semanticContextTrees
            .Select(context => HasSameFilePath(context, tree) ? tree : context)
            .ToArray();
        semanticModel = CreateSemanticModel(tree, semanticContextTrees);

        var apiTransform = new ApiCallHidingTransform(
            callerClass, callerMethod, callerNs, seed);
        tree = apiTransform.Rewrite(tree, semanticModel);
        return strTransform.Rewrite(tree);
    }

    private static SemanticModel CreateSemanticModel(
        SyntaxTree tree,
        IReadOnlyList<SyntaxTree> pluginContractTrees)
    {
        var trees = pluginContractTrees
            .Where(contract => !HasSameFilePath(contract, tree))
            .Prepend(tree);
        var compilation = CreateLibraryCompilation(
            $"SourceRewrite_{Guid.NewGuid():N}", trees);
        return compilation.GetSemanticModel(tree, ignoreAccessibility: true);
    }

    private static CSharpCompilation CreateLibraryCompilation(
        string assemblyName, IEnumerable<SyntaxTree> syntaxTrees) =>
        CSharpCompilation.Create(
            assemblyName,
            syntaxTrees.Append(ContractScanner.ImplicitUsingsTree),
            ContractScanner.PlatformReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static bool HasSameFilePath(SyntaxTree left, SyntaxTree right) =>
        PathIdentity.Comparer.Equals(
            PathIdentity.Normalize(left.FilePath),
            PathIdentity.Normalize(right.FilePath));

    private static (string decNs, string decClass, string decMethod,
                    string calNs, string calClass, string calMethod)
        GenerateHelperNames(int seed)
    {
        var rng = new Random(seed);
        var used = new HashSet<string>();

        var decNs = GenerateUniqueName(rng, used, 8);
        var decClass = GenerateUniqueName(rng, used, 8);
        var decMethod = GenerateUniqueName(rng, used, 8);
        var calNs = GenerateUniqueName(rng, used, 8);
        var calClass = GenerateUniqueName(rng, used, 8);
        var calMethod = GenerateUniqueName(rng, used, 8);

        return (decNs, decClass, decMethod, calNs, calClass, calMethod);
    }

    private static string GenerateUniqueName(Random rng, HashSet<string> used, int length)
    {
        while (true)
        {
            var candidate = GenerateCandidate(rng, length);
            if (used.Add(candidate))
                return candidate;
        }
    }

    private static string GenerateCandidate(Random rng, int length)
    {
        var sb = new StringBuilder(length + 1);
        sb.Append('_');
        for (var i = 0; i < length; i++)
            sb.Append(AlphaNumChars[rng.Next(AlphaNumChars.Length)]);
        return sb.ToString();
    }

    private static void RestoreGeneratedFiles(
        IReadOnlyDictionary<string, byte[]?> originals)
    {
        foreach (var (path, bytes) in originals)
        {
            if (bytes is null)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            else
            {
                File.WriteAllBytes(path, bytes);
            }
        }
    }

    private static void InjectRuntimeHelper(
        string resourceName,
        string outputPath,
        Dictionary<string, string> replacements)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = reader.ReadToEnd();

        foreach (var (token, value) in replacements)
            content = content.Replace(token, value);

        File.WriteAllText(outputPath, content, Encoding.UTF8);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static void WriteDeobfuscationMap(
        ObfuscationConfig config,
        string decryptorNs, string decryptorClass, string decryptorMethod,
        string callerNs, string callerClass, string callerMethod,
        UuidRenameMap? uuidMap)
    {
        var map = new DeobfuscationMap
        {
            Seed = config.Seed,
            Uuid = config.Uuid,
            StringDecryptor = new DeobfuscationMap.HelperInfo(
                decryptorNs, decryptorClass, decryptorMethod),
            IndirectCaller = new DeobfuscationMap.HelperInfo(
                callerNs, callerClass, callerMethod),
            UuidRenames = uuidMap?.GetAllMappings() ?? new Dictionary<string, string>(),
        };

        map.SaveToFile(config.MapPath!);
    }
}
