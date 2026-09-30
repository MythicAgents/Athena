using Agent.Interfaces;
using Agent.Models;

namespace CommandLifecycle.Tests;

internal sealed class RecordingMessageManager : IMessageManager
{
    public List<string> Responses { get; } = new();
    public List<string> CompletedJobs { get; } = new();
    public List<(string Window, string Task, string Key)> Keystrokes { get; } = new();
    public Dictionary<string, ServerJob> Jobs { get; } = new();

    public void AddTaskResponse(ITaskResponse response) => AddTaskResponse(((TaskResponse)response).ToJson());
    public void AddTaskResponse(string response) => Responses.Add(response);
    public void AddTaskResponse(string response, string taskId, bool completed) => AddTaskResponse(response);
    public void Write(string? output, string taskId, bool completed, string status) => Record(output, taskId, completed, status);
    public void Write(string? output, string taskId, bool completed) => Record(output, taskId, completed, string.Empty);
    public void WriteLine(string? output, string taskId, bool completed, string status) => Record(output, taskId, completed, status);
    public void WriteLine(string? output, string taskId, bool completed) => Record(output, taskId, completed, string.Empty);
    public void AddKeystroke(string window, string taskId, string key) => Keystrokes.Add((window, taskId, key));
    public void CompleteJob(string taskId) => CompletedJobs.Add(taskId);
    public bool TryGetJob(string taskId, out ServerJob job)
    {
        if (Jobs.TryGetValue(taskId, out ServerJob? found))
        {
            job = found;
            return true;
        }

        job = null!;
        return false;
    }
    public Dictionary<string, ServerJob> GetJobs() => Jobs;
    public void AddJob(ServerJob job) => Jobs.Add(job.task.id, job);
    public bool HasResponses() => Responses.Count > 0;
    public void AddDelegateMessage(DelegateMessage message) { }
    public void AddInteractMessage(InteractMessage message) { }
    public void AddDatagram(DatagramSource source, ServerDatagram datagram) { }
    public bool TryAddDatagram(DatagramSource source, ServerDatagram datagram) => true;
    public Task<T> DeliverAsync<T>(Func<string, Task<T>> deliver, Func<T, bool> accepted) => deliver(string.Empty);

    private void Record(string? output, string taskId, bool completed, string status) =>
        Responses.Add(new TaskResponse { user_output = output ?? string.Empty, task_id = taskId, completed = completed, status = status }.ToJson());
}

internal sealed class TestAgentConfig : IAgentConfig
{
    public int chunk_size { get; set; } = 16;
    public string? uuid { get; set; }
    public string build_uuid => string.Empty;
    public int sleep { get; set; }
    public int jitter { get; set; }
    public string? psk { get; set; }
    public bool prettyOutput { get; set; }
    public bool debug { get; set; }
    public int inject { get; set; }
    public DateTime killDate { get; set; }
    public event EventHandler? SetAgentConfigUpdated
    {
        add { }
        remove { }
    }
}

internal sealed class RecordingPythonManager : IPythonManager
{
    private readonly byte[]? expectedBytes;
    private readonly bool expectedResult;

    public RecordingPythonManager(byte[]? expectedBytes = null, bool expectedResult = false)
    {
        this.expectedBytes = expectedBytes;
        this.expectedResult = expectedResult;
    }

    public int LoadCalls { get; private set; }
    public byte[] LoadedBytes { get; private set; } = Array.Empty<byte>();
    public bool LoadPyLib(byte[] bytes)
    {
        LoadCalls++;
        LoadedBytes = bytes.ToArray();
        return expectedResult && expectedBytes is not null && bytes.SequenceEqual(expectedBytes);
    }
    public Task<string> ExecuteScriptAsync(string[] args, string script) => Task.FromResult(string.Empty);
    public string ExecuteScript(string script, string[] args) => string.Empty;
    public bool ClearPyLib() => true;
}
