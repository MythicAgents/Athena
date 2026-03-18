using Obfuscator.IL.Transforms;

namespace Obfuscator.IL;

public sealed class ILRewriter
{
    private static readonly string[] SkipPrefixes =
        ["System.", "Microsoft.", "runtime.",
         "Autofac", "IronPython", "BouncyCastle",
         "H.", "Renci", "Mono.", "NamedPipe"];

    public void Rewrite(
        string inputDllPath, int seed, string? mapPath)
    {
        var bytes = File.ReadAllBytes(inputDllPath);
        var searchDir = Path.GetDirectoryName(
            Path.GetFullPath(inputDllPath));

        var mmt = new MetadataManglingTransform(seed);
        bytes = mmt.Transform(bytes, searchDir);

        WriteWithRetry(inputDllPath, bytes);

        if (mapPath is not null)
        {
            var map = File.Exists(mapPath)
                ? DeobfuscationMap.LoadFromFile(mapPath)
                : new DeobfuscationMap();
            map.MetadataRenames = mmt.GetRenameMappings();
            map.SaveToFile(mapPath);
        }
    }

    public void RewriteBatch(
        string directory,
        int seed,
        string? mapPath,
        bool skipFileRename = false,
        bool skipAssemblyRename = false)
    {
        var dllFiles =
            Directory.GetFiles(directory, "*.dll");
        Array.Sort(dllFiles, StringComparer.Ordinal);

        var qualifying = dllFiles
            .Where(f => !ShouldSkip(
                Path.GetFileNameWithoutExtension(f)))
            .ToArray();

        // Step 1: Per-DLL MetadataManglingTransform.
        // All obfuscated bytes are held in memory until the full
        // pass is complete so the search directory always contains
        // original (pre-obfuscation) files while IsInterfaceImpl /
        // BuildVirtualMethodFamilies resolve cross-assembly references.
        var perAssemblyMaps =
            new Dictionary<string,
                Dictionary<string, string>>();
        var obfuscatedBytes =
            new Dictionary<string, byte[]>();

        foreach (var dllPath in qualifying)
        {
            var bytes = File.ReadAllBytes(dllPath);

            // Read assembly identity BEFORE mangling.
            // Skip native DLLs that Cecil cannot parse.
            string asmName;
            try
            {
                using var pre = new MemoryStream(bytes);
                using var preAsm = Mono.Cecil
                    .AssemblyDefinition.ReadAssembly(pre);
                asmName = preAsm.Name.Name;
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            var mmt = new MetadataManglingTransform(seed);
            bytes = mmt.Transform(bytes, directory);
            obfuscatedBytes[dllPath] = bytes;

            perAssemblyMaps[asmName] =
                mmt.GetRenameMappings();
        }

        // Flush all obfuscated assemblies to disk at once now
        // that Step 1 is complete.
        foreach (var (dllPath, bytes) in obfuscatedBytes)
            WriteWithRetry(dllPath, bytes);

        // Step 2: CrossReferenceTransform
        var crossRef = new CrossReferenceTransform();
        foreach (var dllPath in qualifying)
        {
            var bytes = File.ReadAllBytes(dllPath);
            try
            {
                bytes = crossRef.PatchReferences(
                    bytes, perAssemblyMaps, directory);
            }
            catch (BadImageFormatException)
            {
                continue;
            }
            WriteWithRetry(dllPath, bytes);
        }

        // Step 3: AssemblyRenameTransform
        // Skipped for single-file bundles: the .NET bundle host probes by
        // entry filename, not PE identity. Renaming PE identities without
        // also renaming bundle entries causes FileNotFoundException at startup.
        var renameMap = new Dictionary<string, string>();
        if (!skipAssemblyRename)
        {
            var asmRename = new AssemblyRenameTransform(seed);
            renameMap = asmRename.RenameAll(directory, skipFileRename);
        }

        if (mapPath is not null)
        {
            var map = File.Exists(mapPath)
                ? DeobfuscationMap.LoadFromFile(mapPath)
                : new DeobfuscationMap();

            var merged = new Dictionary<string, string>();
            foreach (var (_, asmMap) in perAssemblyMaps)
                foreach (var (k, v) in asmMap)
                    merged.TryAdd(k, v);
            foreach (var (k, v) in renameMap)
                merged.TryAdd("asm:" + k, v);

            map.MetadataRenames = merged;
            map.SaveToFile(mapPath);
        }
    }

    private static bool ShouldSkip(string name)
    {
        foreach (var prefix in SkipPrefixes)
        {
            if (name.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Retries WriteAllBytes on IOException (e.g. transient AV scanner lock).
    private static void WriteWithRetry(string path, byte[] bytes)
    {
        const int MaxAttempts = 5;
        for (int i = 0; i < MaxAttempts; i++)
        {
            try
            {
                File.WriteAllBytes(path, bytes);
                return;
            }
            catch (IOException) when (i < MaxAttempts - 1)
            {
                Thread.Sleep(200 * (i + 1));
            }
        }
    }
}
