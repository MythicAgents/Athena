using Microsoft.NET.HostModel.Bundle;
using Obfuscator.IL.Transforms;

namespace Obfuscator.IL;

/// <summary>
/// Post-processes a single-file bundle exe: extracts embedded assemblies,
/// renames their identities using AssemblyRenameTransform, then re-bundles
/// back into the original exe path.
/// </summary>
public sealed class BundlePatcher
{
    private readonly int _seed;

    public BundlePatcher(int seed)
    {
        _seed = seed;
    }

    /// <summary>
    /// Patches the given single-file bundle exe in-place.
    /// </summary>
    /// <param name="inputExe">Absolute path to the self-contained single-file exe.</param>
    /// <param name="mapPath">Optional path to write/merge a <see cref="DeobfuscationMap"/>.</param>
    /// <returns>The rename map: original assembly name → obfuscated name.</returns>
    public Dictionary<string, string> Patch(string inputExe, string? mapPath = null)
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "obf_bundle_" + Path.GetRandomFileName());

        Directory.CreateDirectory(tempDir);
        try
        {
            ExtractBundle(inputExe, tempDir);

            var entryAssemblyName = FindEntryAssemblyName(inputExe, tempDir);

            var transform = new AssemblyRenameTransform(_seed);
            var renameMap = transform.RenameAll(
                tempDir,
                skipFileRename: false,
                extraSkipNames: entryAssemblyName is not null
                    ? [entryAssemblyName]
                    : null);

            if (mapPath is not null)
            {
                var map = File.Exists(mapPath)
                    ? DeobfuscationMap.LoadFromFile(mapPath)
                    : new DeobfuscationMap();
                var existing = map.MetadataRenames ?? new Dictionary<string, string>();
                foreach (var (k, v) in renameMap)
                    existing.TryAdd("asm:" + k, v);
                map.MetadataRenames = existing;
                map.SaveToFile(mapPath);
            }

            var outDir = Path.Combine(
                Path.GetTempPath(),
                "obf_bundle_out_" + Path.GetRandomFileName());
            Directory.CreateDirectory(outDir);
            try
            {
                var fileSpecs = BuildFileSpecs(tempDir);
                var outputPath = new Bundler(
                        entryAssemblyName ?? Path.GetFileNameWithoutExtension(inputExe),
                        outDir,
                        embedPDBs: false,
                        diagnosticOutput: false)
                    .GenerateBundle(fileSpecs);

                File.Move(outputPath, inputExe, overwrite: true);

                Console.WriteLine(
                    $"[patch-bundle] Renamed {renameMap.Count} assemblies. "
                    + $"Entry '{entryAssemblyName}' preserved.");
            }
            finally
            {
                TryDeleteDirectory(outDir);
            }

            return renameMap;
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// Extracts all embedded files from the bundle to <paramref name="destDir"/>.
    /// </summary>
    private static void ExtractBundle(string inputExe, string destDir)
    {
        new Extractor(inputExe, destDir, diagnosticOutput: false)
            .ExtractFiles();
    }

    /// <summary>
    /// Locates the entry assembly name from the .deps.json stem in the extracted
    /// directory. Falls back to the exe basename (without extension) if not found.
    /// The entry assembly is excluded from renaming so the host can locate it.
    /// </summary>
    private static string? FindEntryAssemblyName(string inputExe, string extractedDir)
    {
        var depsFiles = Directory.GetFiles(extractedDir, "*.deps.json");
        if (depsFiles.Length > 0)
            return Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(depsFiles[0]));

        // Fall back to exe basename — likely the entry assembly name
        var exeBaseName = Path.GetFileNameWithoutExtension(inputExe);
        return string.IsNullOrEmpty(exeBaseName) ? null : exeBaseName;
    }

    /// <summary>
    /// Walks all files in <paramref name="tempDir"/> and returns a FileSpec list
    /// with paths relative to that directory for re-bundling.
    /// </summary>
    private static IReadOnlyList<FileSpec> BuildFileSpecs(string tempDir)
    {
        var specs = new List<FileSpec>();
        foreach (var file in Directory.EnumerateFiles(
            tempDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(tempDir, file)
                .Replace('\\', '/');
            specs.Add(new FileSpec(file, relativePath));
        }
        return specs;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
