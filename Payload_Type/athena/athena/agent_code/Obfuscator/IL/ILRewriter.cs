using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Obfuscator.IL.Transforms;

namespace Obfuscator.IL;

public sealed record BatchRewriteOptions(
    int Seed,
    string? MapPath,
    IReadOnlyCollection<string> FirstPartyAssemblyNames,
    bool SkipFileRename = false,
    bool SkipAssemblyRename = false);

public sealed class ILRewriter
{
    private static readonly JsonSerializerOptions MapJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Rewrite(string inputDllPath, int seed, string? mapPath)
    {
        inputDllPath = Path.GetFullPath(inputDllPath);
        var bytes = File.ReadAllBytes(inputDllPath);
        CliSignatureSafety.Validate(bytes, inputDllPath);
        var mmt = new MetadataManglingTransform(seed);
        bytes = mmt.Transform(bytes, Path.GetDirectoryName(inputDllPath));

        var writes = new List<FileRewrite> { new(inputDllPath, inputDllPath, bytes) };
        if (mapPath is not null)
            writes.Add(CreateSingleMapWrite(Path.GetFullPath(mapPath), mmt.GetRenameMappings()));
        FileRewriteTransaction.Commit(writes);
    }

    public void RewriteBatch(
        string directory,
        int seed,
        string? mapPath,
        IReadOnlyCollection<string> firstPartyAssemblyNames,
        bool skipFileRename = false,
        bool skipAssemblyRename = false) =>
        RewriteBatch(
            directory,
            new BatchRewriteOptions(
                seed, mapPath, firstPartyAssemblyNames, skipFileRename, skipAssemblyRename));

    public void RewriteBatch(string directory, BatchRewriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.FirstPartyAssemblyNames);
        directory = Path.GetFullPath(directory);

        var managedIdentities = DiscoverManagedAssemblies(directory);
        var qualifying = GetQualifyingPaths(managedIdentities, options.FirstPartyAssemblyNames);
        if (qualifying.Length == 0)
        {
            _ = SingleFileBundleRewriter.TryRewrite(directory, options, this);
            return;
        }

        ValidateBatchSignatures(qualifying);
        var (depsJsonPath, entryAssemblyName) = ResolveRootManifest(directory, options);
        var (transformedBytes, perAssemblyMaps) = TransformQualifyingAssemblies(
            directory, qualifying, (managedIdentities, options.Seed));
        var renamePlan = PrepareAssemblyRenames(
            directory, (entryAssemblyName, options), transformedBytes);
        var result = new BatchTransformResult(transformedBytes, perAssemblyMaps, renamePlan);
        CommitBatchWrites(depsJsonPath, options.MapPath, result);
    }

    private sealed record BatchTransformResult(
        Dictionary<string, byte[]> TransformedBytes,
        Dictionary<string, Dictionary<string, string>> PerAssemblyMaps,
        AssemblyRenamePlan? RenamePlan);

    private static Dictionary<string, string> DiscoverManagedAssemblies(string directory)
    {
        var managedIdentities = new Dictionary<string, string>(PathIdentity.Comparer);
        foreach (var dllPath in Directory.GetFiles(directory, "*.dll")
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            var identity = ReadManagedIdentity(dllPath);
            if (identity is not null)
                managedIdentities[dllPath] = identity;
        }
        return managedIdentities;
    }

    private static string? ReadManagedIdentity(string dllPath)
    {
        var bytes = File.ReadAllBytes(dllPath);
        if (PeFileClassifier.Classify(bytes, dllPath) == PeFileKind.Native)
            return null;

        try
        {
            using var stream = new MemoryStream(bytes);
            using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(stream);
            return assembly.Name.Name;
        }
        catch (BadImageFormatException ex)
        {
            throw PeFileClassifier.InvalidImage(dllPath, ex);
        }
    }

    private static string[] GetQualifyingPaths(
        Dictionary<string, string> managedIdentities,
        IReadOnlyCollection<string> firstPartyAssemblyNames)
    {
        var firstParty = new HashSet<string>(
            firstPartyAssemblyNames, StringComparer.OrdinalIgnoreCase);
        return managedIdentities
            .Where(pair => firstParty.Contains(pair.Value))
            .Select(pair => pair.Key)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateBatchSignatures(string[] qualifying)
    {
        foreach (var dllPath in qualifying)
            CliSignatureSafety.Validate(File.ReadAllBytes(dllPath), dllPath);
    }

    private static (string? DepsJsonPath, string? EntryAssemblyName) ResolveRootManifest(
        string directory,
        BatchRewriteOptions options)
    {
        if (options.SkipAssemblyRename || options.SkipFileRename)
            return (null, null);

        var depsFiles = Directory.GetFiles(directory, "*.deps.json", SearchOption.TopDirectoryOnly);
        var rcfgFiles = Directory.GetFiles(
            directory, "*.runtimeconfig.json", SearchOption.TopDirectoryOnly);
        if (depsFiles.Length == 0 && rcfgFiles.Length == 0)
            return (null, null);

        return ValidateRootManifestFiles(directory, depsFiles, rcfgFiles);
    }

    private static (string DepsJsonPath, string EntryAssemblyName) ValidateRootManifestFiles(
        string directory,
        string[] depsFiles,
        string[] runtimeConfigFiles)
    {
        if (depsFiles.Length != 1)
            throw new InvalidDataException(
                "Physical assembly renaming requires exactly one root .deps.json manifest.");

        var depsJsonPath = Path.GetFullPath(depsFiles[0]);
        var entryName = Path.GetFileName(depsJsonPath)[..^".deps.json".Length];
        var entryDll = Path.Combine(directory, entryName + ".dll");
        var rcfgPath = Path.Combine(directory, entryName + ".runtimeconfig.json");
        if (!File.Exists(entryDll) || !File.Exists(rcfgPath) || runtimeConfigFiles.Length != 1)
            throw new InvalidDataException(
                $"The root manifest '{Path.GetFileName(depsJsonPath)}' requires matching "
                + $"'{entryName}.dll' and '{entryName}.runtimeconfig.json'.");

        return (depsJsonPath, entryName);
    }

    private static (
        Dictionary<string, byte[]> TransformedBytes,
        Dictionary<string, Dictionary<string, string>> PerAssemblyMaps)
        TransformQualifyingAssemblies(
            string directory,
            string[] qualifying,
            (Dictionary<string, string> Identities, int Seed) context)
    {
        var perAssemblyMaps = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);
        var transformedBytes = new Dictionary<string, byte[]>(PathIdentity.Comparer);
        foreach (var dllPath in qualifying)
        {
            var mmt = new MetadataManglingTransform(context.Seed);
            transformedBytes[dllPath] = mmt.Transform(File.ReadAllBytes(dllPath), directory);
            perAssemblyMaps[context.Identities[dllPath]] = mmt.GetRenameMappings();
        }

        var crossRef = new CrossReferenceTransform();
        foreach (var dllPath in qualifying)
            transformedBytes[dllPath] = crossRef.PatchReferences(
                transformedBytes[dllPath], perAssemblyMaps, directory);
        return (transformedBytes, perAssemblyMaps);
    }

    private static AssemblyRenamePlan? PrepareAssemblyRenames(
        string directory,
        (string? EntryAssemblyName, BatchRewriteOptions Options) context,
        Dictionary<string, byte[]> transformedBytes)
    {
        if (context.Options.SkipAssemblyRename)
            return null;

        return new AssemblyRenameTransform(context.Options.Seed).Prepare(
            directory,
            context.Options.FirstPartyAssemblyNames,
            context.EntryAssemblyName is null ? [] : [context.EntryAssemblyName],
            context.Options.SkipFileRename,
            transformedBytes);
    }

    private static void CommitBatchWrites(
        string? depsJsonPath,
        string? mapPath,
        BatchTransformResult result)
    {
        var finalAssemblies = BuildFinalAssemblyMap(result.TransformedBytes, result.RenamePlan);
        var writes = finalAssemblies.Values
            .Select(file => new FileRewrite(file.OldPath, file.NewPath, file.Bytes))
            .ToList();
        var renameMap = result.RenamePlan?.RenameMap ?? [];

        if (depsJsonPath is not null)
            writes.Add(new FileRewrite(
                depsJsonPath,
                depsJsonPath,
                DepsJsonPatcher.Render(File.ReadAllBytes(depsJsonPath), renameMap)));
        if (mapPath is not null)
            writes.Add(CreateBatchMapWrite(
                Path.GetFullPath(mapPath), result.PerAssemblyMaps, renameMap));

        FileRewriteTransaction.Commit(writes);
    }

    private static Dictionary<string, AssemblyRenameFile> BuildFinalAssemblyMap(
        Dictionary<string, byte[]> transformedBytes,
        AssemblyRenamePlan? renamePlan)
    {
        var finalAssemblies = transformedBytes.ToDictionary(
            pair => pair.Key,
            pair => new AssemblyRenameFile(pair.Key, pair.Key, pair.Value),
            PathIdentity.Comparer);
        if (renamePlan is null)
            return finalAssemblies;

        foreach (var file in renamePlan.Files)
            finalAssemblies[file.OldPath] = file;
        return finalAssemblies;
    }

    private static FileRewrite CreateSingleMapWrite(
        string mapPath,
        Dictionary<string, string> renames)
    {
        var map = File.Exists(mapPath)
            ? DeobfuscationMap.LoadFromFile(mapPath)
            : new DeobfuscationMap();
        map.MetadataRenames = renames;
        return new FileRewrite(File.Exists(mapPath) ? mapPath : null, mapPath, RenderMap(map));
    }

    private static FileRewrite CreateBatchMapWrite(
        string mapPath,
        Dictionary<string, Dictionary<string, string>> perAssemblyMaps,
        Dictionary<string, string> renameMap)
    {
        var merged = new Dictionary<string, string>();
        foreach (var (_, asmMap) in perAssemblyMaps)
            foreach (var (key, value) in asmMap)
                merged.TryAdd(key, value);
        foreach (var (key, value) in renameMap)
            merged.TryAdd("asm:" + key, value);
        return CreateSingleMapWrite(mapPath, merged);
    }

    private static byte[] RenderMap(DeobfuscationMap map)
    {
        var json = JsonSerializer.Serialize(map, MapJsonOptions);
        _ = JsonSerializer.Deserialize<DeobfuscationMap>(json, MapJsonOptions)
            ?? throw new JsonException("Rendered deobfuscation map was empty.");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
    }
}
