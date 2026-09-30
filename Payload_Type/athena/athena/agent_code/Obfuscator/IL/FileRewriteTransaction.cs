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

        var destinations = new HashSet<string>(PathIdentity.Comparer);
        foreach (var item in items)
        {
            if (!destinations.Add(PathIdentity.Normalize(item.NewPath)))
                throw new InvalidDataException(
                    $"Transaction contains duplicate destination '{item.NewPath}'.");
        }

        var staged = new List<(FileRewrite Item, string Temp)>();
        var backups = new List<(string Original, string Backup)>();
        var installed = new List<string>();
        try
        {
            foreach (var item in items)
            {
                var destination = Path.GetFullPath(item.NewPath);
                var temp = Path.Combine(
                    Path.GetDirectoryName(destination)!,
                    $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.stage");
                using (var stream = new FileStream(
                    temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(item.Bytes);
                    stream.Flush(flushToDisk: true);
                }
                staged.Add((item, temp));
            }

            foreach (var (item, _) in staged)
            {
                if (item.OldPath is null)
                    continue;
                var original = Path.GetFullPath(item.OldPath);
                var backup = Path.Combine(
                    Path.GetDirectoryName(original)!,
                    $".{Path.GetFileName(original)}.{Guid.NewGuid():N}.backup");
                File.Move(original, backup);
                backups.Add((original, backup));
            }

            foreach (var (item, temp) in staged)
            {
                var destination = Path.GetFullPath(item.NewPath);
                File.Move(temp, destination);
                installed.Add(destination);
            }
        }
        catch (Exception commitFailure)
        {
            var rollbackFailures = new List<Exception>();
            foreach (var destination in installed.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(destination))
                        deleteFile(destination);
                }
                catch (Exception error)
                {
                    rollbackFailures.Add(error);
                }
            }

            foreach (var (original, backup) in backups.AsEnumerable().Reverse())
            {
                try
                {
                    if (!File.Exists(backup))
                        continue;
                    if (File.Exists(original))
                    {
                        rollbackFailures.Add(new IOException(
                            $"Could not restore '{original}' because the destination is occupied; " +
                            $"the original remains at '{backup}'."));
                        continue;
                    }
                    File.Move(backup, original);
                }
                catch (Exception error)
                {
                    rollbackFailures.Add(error);
                }
            }

            foreach (var (_, temp) in staged)
            {
                try
                {
                    if (File.Exists(temp))
                        deleteFile(temp);
                }
                catch (Exception error)
                {
                    rollbackFailures.Add(error);
                }
            }

            if (rollbackFailures.Count == 0)
                throw;

            throw new AggregateException(
                "File rewrite failed and rollback cleanup encountered errors.",
                new[] { commitFailure }.Concat(rollbackFailures));
        }

        foreach (var (_, backup) in backups)
        {
            try
            {
                if (File.Exists(backup))
                    deleteFile(backup);
            }
            catch
            {
                // Outputs are committed. Preserve an undeletable backup as recovery evidence.
            }
        }
    }
}
