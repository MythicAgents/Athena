using System.Threading.Channels;

namespace tail;

public readonly record struct ResolvedTailPath(string FullPath, string Directory, string FileName);

public static class TailPath
{
    public static ResolvedTailPath Resolve(string path, string? currentDirectory = null)
    {
        var fullPath = Path.GetFullPath(path, currentDirectory ?? Environment.CurrentDirectory);
        return new ResolvedTailPath(
            fullPath,
            Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("The path has no directory.", nameof(path)),
            Path.GetFileName(fullPath));
    }
}

public static class TailReader
{
    public static IReadOnlyList<string> ReadLastLines(TextReader reader, int count)
    {
        if (count <= 0) return Array.Empty<string>();
        var lines = new Queue<string>(count);
        while (reader.ReadLine() is { } line)
        {
            if (lines.Count == count) lines.Dequeue();
            lines.Enqueue(line);
        }
        return lines.ToArray();
    }
}

public sealed class TailChangePump : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> readChanges;
    private readonly Channel<bool> changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });

    public TailChangePump(Func<CancellationToken, Task> readChanges) => this.readChanges = readChanges;

    public void Signal() => changes.Writer.TryWrite(true);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await changes.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                changes.Reader.TryRead(out _);
                await readChanges(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        changes.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
