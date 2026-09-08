extern alias uploadplugin;

using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UploadPlugin = uploadplugin::Agent.Plugin;

namespace DataRouting.Tests;

[TestClass]
public sealed class CommandDataRoutingTests
{
    [TestMethod]
    public async Task UploadWritesTheReceivedFileBytes()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "payload.bin");
        byte[] expected = { 0, 1, 2, 0, 255 };
        var messages = new RecordingMessageManager();
        var plugin = new UploadPlugin(messages, new TestConfig { chunk_size = 1024 }, null!, null!, null!, null!);
        var job = new ServerJob(new ServerTask
        {
            id = "upload",
            command = "upload",
            parameters = JsonSerializer.Serialize(new { path = directory, filename = "payload.bin", file = "mythic-file" })
        });

        try
        {
            await plugin.Execute(job);
            await plugin.HandleNextMessage(new ServerTaskingResponse
            {
                task_id = job.task.id,
                total_chunks = 1,
                chunk_num = 1,
                chunk_data = Misc.Base64Encode(expected)
            });

            CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

internal sealed class TestConfig : IAgentConfig
{
    public int chunk_size { get; set; }
    public string? uuid { get; set; }
    public string build_uuid => "test";
    public int sleep { get; set; }
    public int jitter { get; set; }
    public string? psk { get; set; }
    public bool prettyOutput { get; set; }
    public bool debug { get; set; }
    public int inject { get; set; }
    public DateTime killDate { get; set; }
    public event EventHandler? SetAgentConfigUpdated;
}

internal sealed class RecordingMessageManager : IMessageManager
{
    public List<string> Responses { get; } = new();
    public void AddTaskResponse(ITaskResponse response) => Responses.Add(response.ToJson());
    public void AddTaskResponse(string response) => Responses.Add(response);
    public void AddTaskResponse(string response, string taskId, bool completed) => Responses.Add(response);
    public void Write(string? output, string taskId, bool completed, string status) => Responses.Add(output ?? string.Empty);
    public void Write(string? output, string taskId, bool completed) => Responses.Add(output ?? string.Empty);
    public void WriteLine(string? output, string taskId, bool completed, string status) => Responses.Add(output ?? string.Empty);
    public void WriteLine(string? output, string taskId, bool completed) => Responses.Add(output ?? string.Empty);
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
    public bool HasResponses() => Responses.Count > 0;
}
