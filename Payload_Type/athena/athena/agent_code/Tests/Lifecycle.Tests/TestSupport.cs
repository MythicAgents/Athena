using Agent.Interfaces;
using Agent.Models;

namespace Lifecycle.Tests;

internal sealed class RecordingMessageManager : IMessageManager
{
    private readonly object sync = new();
    public List<TaskResponse> Responses { get; } = new();

    public TaskResponse[] Snapshot()
    {
        lock (sync) return Responses.ToArray();
    }

    public void Write(string? output, string task_id, bool completed, string status)
    {
        lock (sync) Responses.Add(new TaskResponse { user_output = output ?? string.Empty, task_id = task_id, completed = completed, status = status });
    }
    public void Write(string? output, string task_id, bool completed) => Write(output, task_id, completed, string.Empty);
    public void WriteLine(string? output, string task_id, bool completed, string status) => Write(output, task_id, completed, status);
    public void WriteLine(string? output, string task_id, bool completed) => Write(output, task_id, completed);
    public void AddTaskResponse(ITaskResponse response) { }
    public void AddTaskResponse(string response) { }
    public void AddTaskResponse(string response, string taskId, bool completed) { }
    public void AddDelegateMessage(DelegateMessage message) { }
    public void AddInteractMessage(InteractMessage message) { }
    public void AddDatagram(DatagramSource source, ServerDatagram datagram) { }
    public bool TryAddDatagram(DatagramSource source, ServerDatagram datagram) => true;
    public void AddKeystroke(string windowTitle, string taskId, string key) { }
    public void AddJob(ServerJob job) { }
    public Dictionary<string, ServerJob> GetJobs() => new();
    public bool TryGetJob(string taskId, out ServerJob job) { job = null!; return false; }
    public void CompleteJob(string taskId) { }
    public Task<T> DeliverAsync<T>(Func<string, Task<T>> deliver, Func<T, bool> accepted) => deliver(string.Empty);
    public bool HasResponses() => Responses.Count != 0;
}

internal static class Jobs
{
    public static ServerJob Create(string id, string parameters = "{}") =>
        new(new ServerTask { id = id, parameters = parameters });
}
