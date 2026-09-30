using Agent.Interfaces;
using Agent.Managers;
using Agent.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace MessageManager.Tests;

[TestClass]
public class MessageManagerDeliveryTests
{
    [TestMethod]
    public void CapacityExhaustionIsLoggedWithoutStoppingTaskOutputProducers()
    {
        var logger = new RecordingLogger();
        var manager = new Agent.Managers.MessageManager(
            logger,
            maxPendingOutboundBytes: 8,
            maxPendingOutboundCount: 1);

        manager.Write("output-too-large", "task", completed: false);

        Assert.IsFalse(manager.HasResponses());
        Assert.IsTrue(logger.Messages.Any(message => message.Contains("capacity", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task DeliveryRetainsImmutablePayloadUntilAcceptedAndSerializesAttempts()
    {
        const string completedTaskId = "completed-task";
        const string newerTaskId = "newer-task";
        var manager = new Agent.Managers.MessageManager(new NullLogger());
        manager.AddJob(new ServerJob(new ServerTask { id = completedTaskId }));
        manager.AddTaskResponse(new TaskResponse
        {
            task_id = completedTaskId,
            user_output = "command completed",
            completed = true
        });

        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? firstPayload = null;
        Task<bool> firstDelivery = manager.DeliverAsync(
            async payload =>
            {
                firstPayload = payload;
                firstStarted.SetResult();
                await finishFirst.Task;
                return false;
            },
            accepted => accepted);

        await firstStarted.Task;
        Assert.IsTrue(manager.HasResponses(), "In-flight work must remain visible while transport is pending.");
        manager.AddTaskResponse(new TaskResponse
        {
            task_id = newerTaskId,
            user_output = "new output",
            completed = false
        });

        var retryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRetry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? retryPayload = null;
        Task<bool> retryDelivery = manager.DeliverAsync(
            async payload =>
            {
                retryPayload = payload;
                retryStarted.SetResult();
                await finishRetry.Task;
                return true;
            },
            accepted => accepted);

        Assert.IsFalse(retryStarted.Task.IsCompleted, "Concurrent delivery must wait for the active attempt.");
        finishFirst.SetResult();
        Assert.IsFalse(await firstDelivery);
        Assert.IsTrue(manager.TryGetJob(completedTaskId, out _), "Rejected delivery must retain completed jobs.");

        await retryStarted.Task;
        Assert.AreEqual(firstPayload, retryPayload, "Retry payload must be byte-identical.");
        using (JsonDocument retry = JsonDocument.Parse(retryPayload!))
        {
            JsonElement responses = retry.RootElement.GetProperty("responses");
            Assert.AreEqual(1, responses.GetArrayLength(), "New output must not be merged into the retry.");
            Assert.AreEqual(completedTaskId, responses[0].GetProperty("task_id").GetString());
        }

        finishRetry.SetResult();
        Assert.IsTrue(await retryDelivery);
        Assert.IsFalse(manager.TryGetJob(completedTaskId, out _), "Accepted retry must remove completed jobs.");

        string? newerPayload = null;
        Assert.IsTrue(await manager.DeliverAsync(
            payload =>
            {
                newerPayload = payload;
                return Task.FromResult(true);
            },
            accepted => accepted));
        using JsonDocument newer = JsonDocument.Parse(newerPayload!);
        JsonElement newerResponses = newer.RootElement.GetProperty("responses");
        Assert.AreEqual(1, newerResponses.GetArrayLength());
        Assert.AreEqual(newerTaskId, newerResponses[0].GetProperty("task_id").GetString());
        Assert.AreEqual("new output", newerResponses[0].GetProperty("user_output").GetString());
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public void SetDebug(bool debug) { }
        public void Log(string message) => Messages.Add(message);
        public void Debug(string message) { }
    }

    private sealed class NullLogger : ILogger
    {
        public void SetDebug(bool debug) { }
        public void Log(string message) { }
        public void Debug(string message) { }
    }
}
