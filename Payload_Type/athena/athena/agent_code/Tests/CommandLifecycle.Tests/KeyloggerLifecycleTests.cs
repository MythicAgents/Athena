extern alias keylogger;

using Agent.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using KeyboardHook = keylogger::Agent.IKeyboardHook;
using KeyloggerPlugin = keylogger::Agent.Plugin;

namespace CommandLifecycle.Tests;

[TestClass]
public sealed class KeyloggerLifecycleTests
{
    [TestMethod]
    public async Task KeyloggerStartsEmitsKeysAndStops()
    {
        var messages = new RecordingMessageManager();
        var hook = new FakeKeyboardHook();
        var plugin = new KeyloggerPlugin(messages, null!, null!, null!, null!, null!, hook);

        await plugin.Execute(Job("keylog", "start"));
        await hook.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        hook.Fire("terminal", "a");
        await plugin.Execute(Job("stop", "stop"));
        await hook.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));

        CollectionAssert.AreEqual(new[] { ("terminal", "keylog", "a") }, messages.Keystrokes);
        Assert.AreEqual(0, hook.SubscriberCount);
    }

    private static ServerJob Job(string id, string action) => new(new ServerTask
    {
        id = id,
        command = "keylogger",
        parameters = JsonSerializer.Serialize(new { action })
    });

    private sealed class FakeKeyboardHook : KeyboardHook
    {
        private Action<string, string>? handlers;
        public int SubscriberCount => handlers?.GetInvocationList().Length ?? 0;
        public TaskCompletionSource Started { get; } = NewCompletion();
        public TaskCompletionSource Stopped { get; } = NewCompletion();

        public event Action<string, string>? KeyPressed
        {
            add => handlers += value;
            remove => handlers -= value;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally { Stopped.TrySetResult(); }
        }

        public void Fire(string window, string key) => handlers?.Invoke(window, key);
        private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
