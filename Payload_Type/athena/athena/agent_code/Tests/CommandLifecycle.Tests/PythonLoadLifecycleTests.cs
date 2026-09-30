extern alias pythonload;

using Agent.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using PythonLoadPlugin = pythonload::Agent.Plugin;

namespace CommandLifecycle.Tests;

[TestClass]
public sealed class PythonLoadLifecycleTests
{
    [TestMethod]
    public async Task PythonLibraryUploadLoadsExactBytes()
    {
        var messages = new RecordingMessageManager();
        byte[] expected = { 0, 1, 2, 127, 128, 255 };
        var python = new RecordingPythonManager(expected, expectedResult: true);
        var plugin = new PythonLoadPlugin(messages, new TestAgentConfig(), null!, null!, null!, python);
        var job = new ServerJob(new ServerTask
        {
            id = "python-load",
            command = "python-load",
            parameters = JsonSerializer.Serialize(new { file = "file-id" })
        });

        await plugin.Execute(job);
        await plugin.HandleNextMessage(Chunk(job.task.id, 2, 1, expected[..3]));
        await plugin.HandleNextMessage(Chunk(job.task.id, 2, 2, expected[3..]));

        Assert.AreEqual(1, python.LoadCalls);
        CollectionAssert.AreEqual(expected, python.LoadedBytes);
        CollectionAssert.Contains(messages.CompletedJobs, job.task.id);
        Assert.IsTrue(messages.Responses.Any(response => response.Contains("Loaded.") && response.Contains("\"completed\":true")));
    }

    private static ServerTaskingResponse Chunk(string id, int totalChunks, int chunkNumber, byte[] bytes) => new()
    {
        task_id = id,
        total_chunks = totalChunks,
        chunk_num = chunkNumber,
        chunk_data = Convert.ToBase64String(bytes)
    };
}
