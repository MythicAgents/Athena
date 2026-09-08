extern alias caffeinate;

using System.Reflection;
using CaffeinatePlugin = caffeinate::Agent.Plugin;

namespace Lifecycle.Tests;

[TestClass]
public class CaffeinateLifecycleTests
{
    [TestMethod]
    public async Task CaffeinateStartsAndStops()
    {
        var messages = new RecordingMessageManager();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, Task> wait = async token =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        ConstructorInfo constructor = typeof(CaffeinatePlugin).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(IMessageManager), typeof(Action), typeof(Func<CancellationToken, Task>) },
            modifiers: null)!;
        var plugin = (CaffeinatePlugin)constructor.Invoke(new object[] { messages, () => { }, wait });

        Task run = plugin.Execute(Jobs.Create("start"));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await plugin.Execute(Jobs.Create("stop"));
        await run.WaitAsync(TimeSpan.FromSeconds(2));

        CollectionAssert.AreEqual(
            new[] { "Keeping PC awake", "Done.", "Letting computer sleep" },
            messages.Snapshot().Select(response => response.user_output).ToArray());
    }
}
