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
    private static readonly Lazy<MetadataReference[]> PlatformReferences = new(
        () => ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray());

    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    public void Rewrite(ObfuscationConfig config)
    {
        if (config.Uuid is not null)
            _ = UuidRenameMap.NormalizeUuid(config.Uuid);

        var inputDir = PathIdentity.Normalize(config.InputPath);
        var outputDir = PathIdentity.Normalize(config.OutputPath);

        if (!PathIdentity.Comparer.Equals(inputDir, outputDir))
        {
            CopyDirectory(inputDir, outputDir);
        }

        var (decryptorNs, decryptorClass, decryptorMethod,
             callerNs, callerClass, callerMethod) = GenerateHelperNames(config.Seed);

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

        var projectDirs = Directory.EnumerateFiles(
                outputDir, "*.csproj", SearchOption.AllDirectories)
            .Select(f => PathIdentity.Normalize(Path.GetDirectoryName(f) ?? outputDir))
            .Distinct(PathIdentity.Comparer)
            .ToList();

        var generatedFiles = new HashSet<string>(
            PathIdentity.Comparer);
        var generatedOriginals = new Dictionary<string, byte[]?>(
            PathIdentity.Comparer);
        var outputDocuments = new Dictionary<string, string>(
            PathIdentity.Comparer);
        UuidRenameMap? uuidMap = null;

        try
        {
            foreach (var projDir in projectDirs)
            {
                var decPath = Path.Combine(
                    projDir, "_generated_decryptor.cs");
                var calPath = Path.Combine(
                    projDir, "_generated_caller.cs");

                generatedOriginals[decPath] = File.Exists(decPath)
                    ? File.ReadAllBytes(decPath) : null;
                generatedOriginals[calPath] = File.Exists(calPath)
                    ? File.ReadAllBytes(calPath) : null;
                InjectRuntimeHelper(
                    "StringDecryptor.cs", decPath,
                    decryptorReplacements);
                InjectRuntimeHelper(
                    "IndirectCaller.cs", calPath,
                    callerReplacements);

                generatedFiles.Add(decPath);
                generatedFiles.Add(calPath);
            }

        var contractsDir = Path.Combine(outputDir, "Agent.Models");
        var contractNames = Directory.Exists(contractsDir)
            ? ContractScanner.Scan(contractsDir)
            : new ContractNames([], [], [], [], []);
        uuidMap = config.Uuid is not null
            ? UuidRenameMap.Derive(config.Uuid, contractNames)
            : null;

        var excludedPrefixes = new[]
        {
            Path.Combine(outputDir, "Tests"),
            Path.Combine(outputDir, "Obfuscator"),
        };

        var sourceFiles = Directory.EnumerateFiles(
                outputDir, "*.cs", SearchOption.AllDirectories)
            .Select(PathIdentity.Normalize)
            .Where(file => !generatedFiles.Contains(file))
            .Where(file => !excludedPrefixes.Any(p =>
                PathIdentity.IsWithin(file, p)))
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
            PathIdentity.Comparer.Equals(
                project, PathIdentity.Normalize(contractsDir)));
        var semanticSupportPaths = new HashSet<string>(
            owningProjects
                .Where(pair => agentModelsProject is not null
                    && PathIdentity.Comparer.Equals(
                        pair.Value, agentModelsProject))
                .Select(pair => pair.Key),
            PathIdentity.Comparer);

        // Earlier transforms replace trees. Keep each owning project's source
        // group current so semantic binding sees siblings without crossing
        // project boundaries.
        foreach (var file in sourceFiles)
        {
            trees[file] = ApplyNonUuidTransforms(
                trees[file],
                config.Seed,
                GetSemanticContextTrees(
                    file, trees, owningProjects, semanticSupportPaths),
                decryptorNs, decryptorClass, decryptorMethod,
                callerNs, callerClass, callerMethod);
        }

        if (uuidMap is not null)
        {
            // Resolve every UUID rename against one immutable semantic snapshot,
            // then replace all trees together. Renaming a declaration must not
            // make references in later files unresolvable.
            var rewritten = new Dictionary<string, SyntaxTree>(
                PathIdentity.Comparer);
            foreach (var file in sourceFiles)
            {
                var tree = trees[file];
                var semanticModel = CreateSemanticModel(tree,
                    GetSemanticContextTrees(
                        file, trees, owningProjects, semanticSupportPaths));
                rewritten[file] = new UuidRenameTransform(uuidMap)
                    .Rewrite(tree, semanticModel);
            }
            foreach (var (file, tree) in rewritten)
                trees[file] = tree;
        }

        outputDocuments = trees.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.GetRoot().ToFullString(),
            PathIdentity.Comparer);
        foreach (var generatedFile in generatedFiles)
            outputDocuments[generatedFile] = File.ReadAllText(generatedFile);

            if (config.EnableBroadSemanticRename)
            {
                if (config.ProjectRoot is null || config.Configuration is null
                    || config.HandlerOS is null || config.CryptoProvider is null
                    || config.Uuid is null)
                    throw new ArgumentException(
                        "Broad semantic renaming requires project root, Configuration, HandlerOS, CryptoProvider, and UUID.");
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
            .Where(contract => projectTrees.All(project =>
                !PathIdentity.Comparer.Equals(
                    PathIdentity.Normalize(project.FilePath),
                    PathIdentity.Normalize(contract.FilePath))))
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

        var compilation = CSharpCompilation.Create(
            "SourceRewriteSemanticSupport", supportTrees,
            PlatformReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var filtered = new SyntaxTree[supportTrees.Count];
        for (var index = 0; index < supportTrees.Count; index++)
        {
            var supportTree = supportTrees[index];
            var model = compilation.GetSemanticModel(
                supportTree, ignoreAccessibility: true);
            var colliding = supportTree.GetRoot().DescendantNodes()
                .Where(node => node is BaseTypeDeclarationSyntax
                    or DelegateDeclarationSyntax)
                .Where(declaration =>
                    model.GetDeclaredSymbol(declaration) is INamedTypeSymbol type
                    && type.TypeKind != TypeKind.Error
                    && projectDeclarations.Contains(
                        ContractScanner.GetMetadataName(type)))
                .ToHashSet();
            var topLevelCollisions = colliding
                .Where(declaration => !declaration.Ancestors().Any(colliding.Contains))
                .ToArray();
            filtered[index] = topLevelCollisions.Length == 0
                ? supportTree
                : supportTree.WithRootAndOptions(
                    supportTree.GetRoot().RemoveNodes(
                        topLevelCollisions,
                        SyntaxRemoveOptions.KeepExteriorTrivia)!,
                    supportTree.Options);
        }
        return filtered;
    }

    private static HashSet<string> GetDeclaredMetadataNames(
        IReadOnlyList<SyntaxTree> syntaxTrees)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (syntaxTrees.Count == 0)
            return result;
        var compilation = CSharpCompilation.Create(
            "SourceRewriteDeclarations", syntaxTrees,
            PlatformReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        foreach (var syntaxTree in syntaxTrees)
        {
            var model = compilation.GetSemanticModel(
                syntaxTree, ignoreAccessibility: true);
            foreach (var declaration in syntaxTree.GetRoot().DescendantNodes()
                .Where(node => node is BaseTypeDeclarationSyntax
                    or DelegateDeclarationSyntax))
            {
                if (model.GetDeclaredSymbol(declaration) is INamedTypeSymbol type
                    && type.TypeKind != TypeKind.Error)
                    result.Add(ContractScanner.GetMetadataName(type));
            }
        }
        return result;
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
            .Select(context => PathIdentity.Comparer.Equals(
                PathIdentity.Normalize(context.FilePath),
                PathIdentity.Normalize(tree.FilePath)) ? tree : context)
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
        var trees = new List<SyntaxTree> { tree };
        trees.AddRange(pluginContractTrees.Where(contract =>
            !PathIdentity.Comparer.Equals(
                PathIdentity.Normalize(contract.FilePath),
                PathIdentity.Normalize(tree.FilePath))));
        var compilation = CSharpCompilation.Create(
            $"SourceRewrite_{Guid.NewGuid():N}",
            trees,
            PlatformReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSemanticModel(tree, ignoreAccessibility: true);
    }

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
