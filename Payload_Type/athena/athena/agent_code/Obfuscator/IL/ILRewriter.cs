using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Obfuscator.IL.Transforms;

namespace Obfuscator.IL;

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
        var searchDir = Path.GetDirectoryName(inputDllPath);
        var mmt = new MetadataManglingTransform(seed);
        bytes = mmt.Transform(bytes, searchDir);

        var writes = new List<FileRewrite>
        {
            new(inputDllPath, inputDllPath, bytes),
        };
        if (mapPath is not null)
        {
            mapPath = Path.GetFullPath(mapPath);
            var map = File.Exists(mapPath)
                ? DeobfuscationMap.LoadFromFile(mapPath)
                : new DeobfuscationMap();
            map.MetadataRenames = mmt.GetRenameMappings();
            writes.Add(new FileRewrite(
                File.Exists(mapPath) ? mapPath : null,
                mapPath,
                RenderMap(map)));
        }
        FileRewriteTransaction.Commit(writes);
    }

    public void RewriteBatch(
        string directory,
        int seed,
        string? mapPath,
        IReadOnlyCollection<string> firstPartyAssemblyNames,
        bool skipFileRename = false,
        bool skipAssemblyRename = false)
    {
        ArgumentNullException.ThrowIfNull(firstPartyAssemblyNames);
        directory = Path.GetFullPath(directory);
        var firstParty = new HashSet<string>(
            firstPartyAssemblyNames, StringComparer.OrdinalIgnoreCase);
        string? depsJsonPath = null;
        string? entryAssemblyName = null;
        var dllFiles = Directory.GetFiles(directory, "*.dll")
            .Select(Path.GetFullPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var managedIdentities = new Dictionary<string, string>(
            PathIdentity.Comparer);
        foreach (var dllPath in dllFiles)
        {
            var bytes = File.ReadAllBytes(dllPath);
            if (PeFileClassifier.Classify(bytes, dllPath) == PeFileKind.Native)
                continue;

            try
            {
                using var stream = new MemoryStream(bytes);
                using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(stream);
                managedIdentities[dllPath] = assembly.Name.Name;
            }
            catch (BadImageFormatException ex)
            {
                throw PeFileClassifier.InvalidImage(dllPath, ex);
            }
        }

        var qualifying = managedIdentities
            .Where(pair => firstParty.Contains(pair.Value))
            .Select(pair => pair.Key)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        // Validate the complete batch before transforms or output preparation. Cecil 0.11.6
        // cannot round-trip this distinct CLI signature and would silently emit SZARRAY.
        foreach (var dllPath in qualifying)
            CliSignatureSafety.Validate(File.ReadAllBytes(dllPath), dllPath);

        if (!skipAssemblyRename && !skipFileRename)
        {
            var depsFiles = Directory.GetFiles(
                directory, "*.deps.json", SearchOption.TopDirectoryOnly);
            var runtimeConfigFiles = Directory.GetFiles(
                directory, "*.runtimeconfig.json", SearchOption.TopDirectoryOnly);
            if (depsFiles.Length > 0 || runtimeConfigFiles.Length > 0)
            {
                if (depsFiles.Length != 1)
                    throw new InvalidDataException(
                        "Physical assembly renaming requires exactly one root .deps.json manifest.");

                depsJsonPath = Path.GetFullPath(depsFiles[0]);
                entryAssemblyName = Path.GetFileName(depsJsonPath)[..^".deps.json".Length];
                var entryDll = Path.Combine(directory, entryAssemblyName + ".dll");
                var runtimeConfig = Path.Combine(
                    directory, entryAssemblyName + ".runtimeconfig.json");
                if (!File.Exists(entryDll) || !File.Exists(runtimeConfig)
                    || runtimeConfigFiles.Length != 1)
                    throw new InvalidDataException(
                        $"The root manifest '{Path.GetFileName(depsJsonPath)}' requires matching "
                        + $"'{entryAssemblyName}.dll' and '{entryAssemblyName}.runtimeconfig.json'.");
            }
        }

        var perAssemblyMaps = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);
        var transformedBytes = new Dictionary<string, byte[]>(PathIdentity.Comparer);

        foreach (var dllPath in qualifying)
        {
            var mmt = new MetadataManglingTransform(seed);
            var bytes = mmt.Transform(File.ReadAllBytes(dllPath), directory);
            transformedBytes[dllPath] = bytes;
            perAssemblyMaps[managedIdentities[dllPath]] = mmt.GetRenameMappings();
        }

        var crossRef = new CrossReferenceTransform();
        foreach (var dllPath in qualifying)
            transformedBytes[dllPath] = crossRef.PatchReferences(
                transformedBytes[dllPath], perAssemblyMaps, directory);

        var renameMap = new Dictionary<string, string>();
        AssemblyRenamePlan? renamePlan = null;
        if (!skipAssemblyRename)
        {
            renamePlan = new AssemblyRenameTransform(seed).Prepare(
                directory,
                firstPartyAssemblyNames,
                entryAssemblyName is null ? [] : [entryAssemblyName],
                skipFileRename,
                transformedBytes);
            renameMap = renamePlan.RenameMap;
        }

        var finalAssemblies = transformedBytes.ToDictionary(
            pair => pair.Key,
            pair => new AssemblyRenameFile(pair.Key, pair.Key, pair.Value),
            PathIdentity.Comparer);
        if (renamePlan is not null)
        {
            foreach (var file in renamePlan.Files)
                finalAssemblies[file.OldPath] = file;
        }

        var writes = finalAssemblies.Values
            .Select(file => new FileRewrite(file.OldPath, file.NewPath, file.Bytes))
            .ToList();
        if (depsJsonPath is not null)
        {
            var depsBytes = DepsJsonPatcher.Render(
                File.ReadAllBytes(depsJsonPath), renameMap);
            writes.Add(new FileRewrite(depsJsonPath, depsJsonPath, depsBytes));
        }

        if (mapPath is not null)
        {
            mapPath = Path.GetFullPath(mapPath);
            var map = File.Exists(mapPath)
                ? DeobfuscationMap.LoadFromFile(mapPath)
                : new DeobfuscationMap();
            var merged = new Dictionary<string, string>();
            foreach (var (_, asmMap) in perAssemblyMaps)
                foreach (var (key, value) in asmMap)
                    merged.TryAdd(key, value);
            foreach (var (key, value) in renameMap)
                merged.TryAdd("asm:" + key, value);
            map.MetadataRenames = merged;
            writes.Add(new FileRewrite(
                File.Exists(mapPath) ? mapPath : null,
                mapPath,
                RenderMap(map)));
        }

        FileRewriteTransaction.Commit(writes);
    }

    private static byte[] RenderMap(DeobfuscationMap map)
    {
        var json = JsonSerializer.Serialize(map, MapJsonOptions);
        _ = JsonSerializer.Deserialize<DeobfuscationMap>(json, MapJsonOptions)
            ?? throw new JsonException("Rendered deobfuscation map was empty.");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
    }
}
