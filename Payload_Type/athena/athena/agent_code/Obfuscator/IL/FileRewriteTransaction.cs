namespace Obfuscator.IL;

internal sealed record FileRewrite(string? OldPath, string NewPath, byte[] Bytes);

internal static class FileRewriteTransaction
{
    public static void Commit(IEnumerable<FileRewrite> rewrites) =>
        Commit(rewrites, File.Delete);

    internal static void Commit(IEnumerable<FileRewrite> rewrites, Action<string> deleteFile)
    {
        var items = rewrites.ToArray();
        if (items.Length == 0)
            return;

        ValidateUniqueDestinations(items);

        var staged = new List<(FileRewrite Item, string Temp)>();
        var backups = new List<(string Original, string Backup)>();
        var installed = new List<string>();
        try
        {
            StageRewrites(items, staged);
            BackupExistingFiles(staged, backups);
            InstallStagedFiles(staged, installed);
        }
        catch (Exception commitFailure)
        {
            Rollback(installed, backups, staged, deleteFile, commitFailure);
            throw;
        }

        DeleteBackups(backups, deleteFile);
    }

    private static void ValidateUniqueDestinations(IReadOnlyList<FileRewrite> items)
    {
        var destinations = new HashSet<string>(PathIdentity.Comparer);
        foreach (var item in items)
        {
            if (!destinations.Add(PathIdentity.Normalize(item.NewPath)))
                throw new InvalidDataException(
                    $"Transaction contains duplicate destination '{item.NewPath}'.");
        }
    }

    private static void StageRewrites(
        IReadOnlyList<FileRewrite> items,
        List<(FileRewrite Item, string Temp)> staged)
    {
        foreach (var item in items)
        {
            var destination = Path.GetFullPath(item.NewPath);
            var temp = CreateUniqueSiblingPath(destination, "stage");
            using (var stream = new FileStream(
                temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(item.Bytes);
                stream.Flush(flushToDisk: true);
            }
            staged.Add((item, temp));
        }
    }

    private static void BackupExistingFiles(
        IReadOnlyList<(FileRewrite Item, string Temp)> staged,
        List<(string Original, string Backup)> backups)
    {
        foreach (var (item, _) in staged)
        {
            if (item.OldPath is null)
                continue;
            var original = Path.GetFullPath(item.OldPath);
            var backup = CreateUniqueSiblingPath(original, "backup");
            File.Move(original, backup);
            backups.Add((original, backup));
        }
    }

    private static void InstallStagedFiles(
        IReadOnlyList<(FileRewrite Item, string Temp)> staged,
        List<string> installed)
    {
        foreach (var (item, temp) in staged)
        {
            var destination = Path.GetFullPath(item.NewPath);
            File.Move(temp, destination);
            installed.Add(destination);
        }
    }

    private static string CreateUniqueSiblingPath(string path, string suffix) =>
        Path.Combine(
            Path.GetDirectoryName(path)!,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.{suffix}");

    private static void Rollback(
        IReadOnlyList<string> installed,
        IReadOnlyList<(string Original, string Backup)> backups,
        IReadOnlyList<(FileRewrite Item, string Temp)> staged,
        Action<string> deleteFile,
        Exception commitFailure)
    {
        var rollbackFailures = new List<Exception>();

        foreach (var destination in installed.Reverse())
            TryDeleteFile(destination, deleteFile, rollbackFailures);

        foreach (var (original, backup) in backups.Reverse())
            TryRestoreBackup(original, backup, rollbackFailures);

        foreach (var (_, temp) in staged)
            TryDeleteFile(temp, deleteFile, rollbackFailures);

        if (rollbackFailures.Count > 0)
        {
            throw new AggregateException(
                "File rewrite failed and rollback cleanup encountered errors.",
                new[] { commitFailure }.Concat(rollbackFailures));
        }
    }

    private static void TryRestoreBackup(
        string original, string backup, List<Exception> rollbackFailures)
    {
        try
        {
            if (!File.Exists(backup))
                return;
            if (File.Exists(original))
            {
                rollbackFailures.Add(new IOException(
                    $"Could not restore '{original}' because the destination is occupied; " +
                    $"the original remains at '{backup}'."));
                return;
            }
            File.Move(backup, original);
        }
        catch (Exception error)
        {
            rollbackFailures.Add(error);
        }
    }

    private static void TryDeleteFile(
        string path, Action<string> deleteFile, List<Exception>? failures = null)
    {
        try
        {
            if (File.Exists(path))
                deleteFile(path);
        }
        catch (Exception error)
        {
            failures?.Add(error);
        }
    }

    private static void DeleteBackups(
        IReadOnlyList<(string Original, string Backup)> backups,
        Action<string> deleteFile)
    {
        foreach (var (_, backup) in backups)
        {
            // Outputs are committed. Preserve an undeletable backup as recovery evidence.
            TryDeleteFile(backup, deleteFile);
        }
    }
}
