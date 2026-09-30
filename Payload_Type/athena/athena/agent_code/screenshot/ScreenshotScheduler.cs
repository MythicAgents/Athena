using System.Collections.Concurrent;

namespace screenshot;

public sealed class ScreenshotScheduler : IAsyncDisposable
{
    private readonly Func<string, CancellationToken, Task> capture;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly ConcurrentDictionary<string, ScheduledCapture> schedules = new();

    public ScreenshotScheduler(
        Func<string, CancellationToken, Task> capture,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.capture = capture;
        this.delay = delay ?? Task.Delay;
    }

    public int ScheduledTaskCount => schedules.Count;

    public void Schedule(
        string taskId,
        TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        var replacement = new ScheduledCapture(cancellationToken);
        while (true)
        {
            if (schedules.TryGetValue(taskId, out var previous))
            {
                if (!schedules.TryUpdate(taskId, replacement, previous)) continue;
                previous.CancelAndDisposeWhenComplete();
                break;
            }

            if (schedules.TryAdd(taskId, replacement)) break;
        }

        replacement.Start(() => RunAsync(taskId, replacement, interval));
    }

    public void Cancel(string taskId)
    {
        if (schedules.TryRemove(taskId, out var schedule))
        {
            schedule.CancelAndDisposeWhenComplete();
        }
    }

    private async Task RunAsync(string taskId, ScheduledCapture schedule, TimeSpan interval)
    {
        try
        {
            while (true)
            {
                await delay(interval, schedule.Token).ConfigureAwait(false);
                await capture(taskId, schedule.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (schedule.Token.IsCancellationRequested)
        {
        }
        finally
        {
            schedules.TryRemove(new KeyValuePair<string, ScheduledCapture>(taskId, schedule));
            schedule.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var active = schedules.ToArray();
        schedules.Clear();
        foreach (var pair in active) pair.Value.Cancel();
        await Task.WhenAll(active.Select(pair => pair.Value.Completion)).ConfigureAwait(false);
        foreach (var pair in active) pair.Value.Dispose();
    }

    private sealed class ScheduledCapture : IDisposable
    {
        private readonly object gate = new();
        private readonly CancellationTokenSource cancellation;
        private Task completion = Task.CompletedTask;
        private bool started;
        private bool disposeWhenComplete;
        private int disposed;

        public ScheduledCapture(CancellationToken cancellationToken) =>
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        public CancellationToken Token => cancellation.Token;
        public Task Completion { get { lock (gate) return completion; } }

        public void Start(Func<Task> run)
        {
            lock (gate)
            {
                completion = Task.Run(run);
                started = true;
                if (disposeWhenComplete) DisposeAfter(completion);
            }
        }

        public void Cancel() => cancellation.Cancel();

        public void CancelAndDisposeWhenComplete()
        {
            Cancel();
            lock (gate)
            {
                disposeWhenComplete = true;
                if (started) DisposeAfter(completion);
            }
        }

        private void DisposeAfter(Task task) => _ = task.ContinueWith(
            _ => Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) cancellation.Dispose();
        }
    }
}
