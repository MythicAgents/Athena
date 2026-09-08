using System.Collections.Concurrent;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;
using Microsoft.Win32.SafeHandles;

namespace CommandReliability.Tests;

internal sealed class RecordingMessageManager : IMessageManager
{
    private readonly ConcurrentQueue<TaskResponse> responses = new();
    private readonly ConcurrentQueue<string> completedJobs = new();
    private readonly ConcurrentQueue<InteractMessage> interactions = new();

    public TaskResponse[] Responses => responses.ToArray();
    public string[] CompletedJobs => completedJobs.ToArray();
    public InteractMessage[] Interactions => interactions.ToArray();
    public Action? BeforeWriteLine { get; set; }

    public void AddTaskResponse(ITaskResponse response) => Record(response);

    public void AddTaskResponse(string response)
    {
        using JsonDocument json = JsonDocument.Parse(response);
        JsonElement root = json.RootElement;
        responses.Enqueue(new TaskResponse
        {
            task_id = GetString(root, "task_id"),
            user_output = GetString(root, "user_output"),
            status = GetString(root, "status"),
            completed = root.TryGetProperty("completed", out JsonElement completed) && completed.GetBoolean()
        });
    }

    public void AddTaskResponse(string response, string taskId, bool completed) =>
        responses.Enqueue(new TaskResponse { task_id = taskId, user_output = response, completed = completed });

    public void Write(string? output, string taskId, bool completed, string status) =>
        responses.Enqueue(new TaskResponse { task_id = taskId, user_output = output ?? string.Empty, completed = completed, status = status });

    public void Write(string? output, string taskId, bool completed) => Write(output, taskId, completed, string.Empty);
    public void WriteLine(string? output, string taskId, bool completed, string status)
    {
        BeforeWriteLine?.Invoke();
        Write(output, taskId, completed, status);
    }
    public void WriteLine(string? output, string taskId, bool completed) => Write(output, taskId, completed);
    public void CompleteJob(string taskId) => completedJobs.Enqueue(taskId);

    public async Task<TaskResponse> WaitForTerminalResponse(string taskId, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            TaskResponse? response = Responses.LastOrDefault(item => item.task_id == taskId && item.completed);
            if (response is not null) return response;
            await Task.Delay(10);
        }

        Assert.Fail($"No terminal response was recorded for task '{taskId}'.");
        throw new InvalidOperationException();
    }

    private void Record(ITaskResponse response) => responses.Enqueue(new TaskResponse
    {
        task_id = response.task_id,
        user_output = response.user_output,
        status = response.status,
        completed = response.completed
    });

    private static string GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    public void AddDelegateMessage(DelegateMessage message) { }
    public void AddInteractMessage(InteractMessage message) => interactions.Enqueue(message);
    public void AddDatagram(DatagramSource source, ServerDatagram datagram) { }
    public bool TryAddDatagram(DatagramSource source, ServerDatagram datagram) => true;
    public void AddKeystroke(string windowTitle, string taskId, string key) { }
    public void AddJob(ServerJob job) { }
    public Dictionary<string, ServerJob> GetJobs() => new();
    public bool TryGetJob(string taskId, out ServerJob job) { job = null!; return false; }
    public Task<T> DeliverAsync<T>(Func<string, Task<T>> deliver, Func<T, bool> accepted) => deliver(string.Empty);
    public bool HasResponses() => !responses.IsEmpty;
}

internal sealed class TestConfig : IAgentConfig
{
    public int chunk_size { get; set; } = 512_000;
    public string? uuid { get; set; }
    public string build_uuid { get; } = Guid.NewGuid().ToString();
    public int sleep { get; set; } = 10;
    public int jitter { get; set; } = 10;
    public string? psk { get; set; }
    public bool prettyOutput { get; set; }
    public bool debug { get; set; }
    public int inject { get; set; }
    public DateTime killDate { get; set; } = DateTime.UtcNow.AddYears(1);
    public event EventHandler? SetAgentConfigUpdated;
}

internal sealed class StubSpawner(bool result) : ISpawner
{
    public Task<bool> Spawn(SpawnOptions options) => Task.FromResult(result);
    public bool TryGetHandle(string taskId, out SafeProcessHandle? handle) { handle = null; return false; }
}

internal static class TestJobs
{
    public static ServerJob Create(string id, object parameters) =>
        new(new ServerTask { id = id, parameters = JsonSerializer.Serialize(parameters) });
}
