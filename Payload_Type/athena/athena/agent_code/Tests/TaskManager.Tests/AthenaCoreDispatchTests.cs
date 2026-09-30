using Agent;
using Agent.Interfaces;
using Agent.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace TaskManager.Tests;

[TestClass]
public class AthenaCoreDispatchTests
{
    [TestMethod]
    public async Task ProcessingTaskingAwaitsManagerWorkAndAcceptsNullSocks()
    {
        ITaskManager tasks = DispatchProxy.Create<ITaskManager, TaskManagerProxy>();
        var recording = (TaskManagerProxy)(object)tasks;
        var core = new AthenaCore(
            new[] { Stub<IProfile>() },
            tasks,
            Stub<ILogger>(),
            Stub<IAgentConfig>(),
            Stub<ITokenManager>(),
            Array.Empty<IAgentMod>());
        var args = new TaskingReceivedArgs(new GetTaskingResponse
        {
            tasks = new List<ServerTask> { null!, new() { id = "task", command = "demo", parameters = "{}" } },
            socks = null!,
        });

        MethodInfo process = typeof(AthenaCore).GetMethod("ProcessTaskingAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task processing = (Task)process.Invoke(core, new object[] { args })!;

        Assert.IsFalse(processing.IsCompleted);
        Assert.AreEqual(1, recording.StartCalls);
        Assert.AreEqual(0, recording.ProxyCalls);
        recording.Release.SetResult();
        await processing.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static T Stub<T>() where T : class => DispatchProxy.Create<T, DefaultProxy>();

    public class TaskManagerProxy : DispatchProxy
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int StartCalls { get; private set; }
        public int ProxyCalls { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ITaskManager.StartTaskAsync))
            {
                StartCalls++;
                return Release.Task;
            }
            if (targetMethod?.Name == nameof(ITaskManager.HandleProxyResponses))
            {
                ProxyCalls++;
                return Task.CompletedTask;
            }
            return DefaultValue(targetMethod?.ReturnType);
        }
    }

    public class DefaultProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => DefaultValue(targetMethod?.ReturnType);
    }

    private static object? DefaultValue(Type? type) =>
        type is null || type == typeof(void) ? null :
        type == typeof(Task) ? Task.CompletedTask :
        type.IsValueType ? Activator.CreateInstance(type) : null;
}