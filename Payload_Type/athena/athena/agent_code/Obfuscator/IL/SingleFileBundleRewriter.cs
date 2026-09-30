namespace Obfuscator.IL;

/// <summary>
/// Unpacks a .NET single-file bundle in a publish directory, runs the batch IL
/// rewriter across its embedded assemblies and dependency manifest, and repacks
/// the bundle atomically.
/// </summary>
internal static class SingleFileBundleRewriter
{
    private const string AssemblyRenamePrefix = "asm:";

    private sealed record BundleWorkspace(string BundlePath, string TempDir);

    public static bool TryRewrite(
        string directory,
        BatchRewriteOptions options,
        ILRewriter rewriter)
    {
        var bundlePath = FindBundleExecutable(directory);
        if (bundlePath is null)
            return false;

        var tempDir = Path.Combine(
            Path.GetTempPath(), "obf_bundle_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            RewriteBundle(new BundleWorkspace(bundlePath, tempDir), options, rewriter);
            return true;
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static string? FindBundleExecutable(string directory)
    {
        foreach (var filePath in Directory.GetFiles(directory)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            if (IsCandidateHostFile(filePath)
                && SingleFileBundleFormat.IsBundle(File.ReadAllBytes(filePath)))
                return Path.GetFullPath(filePath);
        }
        return null;
    }

    private static bool IsCandidateHostFile(string filePath) =>
        !filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        && !filePath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
        && !filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        && !filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static void RewriteBundle(
        BundleWorkspace workspace,
        BatchRewriteOptions options,
        ILRewriter rewriter)
    {
        var unixMode = OperatingSystem.IsWindows()
            ? (UnixFileMode?)null
            : File.GetUnixFileMode(workspace.BundlePath);
        var originalBytes = File.ReadAllBytes(workspace.BundlePath);
        var manifest = SingleFileBundleFormat.ReadManifest(originalBytes);
        StageBundleEntries(originalBytes, manifest.Entries, workspace.TempDir);

        var mapPath = options.MapPath ?? Path.Combine(workspace.TempDir, "bundle-map.json");
        rewriter.RewriteBatch(
            workspace.TempDir,
            options with { MapPath = mapPath, SkipFileRename = options.SkipAssemblyRename });

        var renames = LoadAssemblyRenames(mapPath);
        var repacked = SingleFileBundleFormat.RebuildBundle(
            new BundleRepackContext(originalBytes, manifest, workspace.TempDir, renames));
        CommitBundle(workspace.BundlePath, repacked, unixMode);
    }

    private static void StageBundleEntries(
        byte[] bundleBytes,
        IReadOnlyList<BundleEntry> entries,
        string tempDir)
    {
        foreach (var entry in entries.Where(item => item.IsStageable))
        {
            var rawBytes = SingleFileBundleFormat.ExtractEntryBytes(bundleBytes, entry);
            File.WriteAllBytes(Path.Combine(tempDir, entry.RelativePath), rawBytes);
        }
    }

    private static Dictionary<string, string> LoadAssemblyRenames(string mapPath)
    {
        if (!File.Exists(mapPath))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var renames = DeobfuscationMap.LoadFromFile(mapPath).MetadataRenames;
        if (renames is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return renames
            .Where(pair => pair.Key.StartsWith(AssemblyRenamePrefix, StringComparison.Ordinal))
            .ToDictionary(
                pair => pair.Key[AssemblyRenamePrefix.Length..],
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
    }

    private static void CommitBundle(
        string bundlePath,
        byte[] repackedBytes,
        UnixFileMode? unixMode)
    {
        FileRewriteTransaction.Commit([new FileRewrite(bundlePath, bundlePath, repackedBytes)]);
        if (!OperatingSystem.IsWindows() && unixMode.HasValue)
            File.SetUnixFileMode(bundlePath, unixMode.Value);
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
