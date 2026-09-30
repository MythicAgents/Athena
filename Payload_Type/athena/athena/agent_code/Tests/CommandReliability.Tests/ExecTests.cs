extern alias execplugin;

using Agent.Interfaces;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;

namespace CommandReliability.Tests;

[TestClass]
public sealed class ExecTests
{
    [TestMethod]
    public async Task ExecPreservesSpawnRequestAndRunsLocalProcess()
    {
        var messages = new RecordingMessageManager();
        var spawner = new RecordingLocalProcessSpawner();
        var plugin = new execplugin::Agent.Plugin(messages, new TestConfig(), null!, null!, spawner, null!);
        string outputPath = Path.Combine(Path.GetTempPath(), $"athena-exec-{Guid.NewGuid():N}");
        string commandline = $"/usr/bin/touch {outputPath}";

        try
        {
            await plugin.Execute(TestJobs.Create("exec-local", new
            {
                parent = 123,
                commandline,
                spoofedcommandline = "visible-to-test",
                output = false,
                suspended = false
            }));

            Assert.IsTrue(File.Exists(outputPath));
            SpawnOptions request = spawner.Requests.Single();
            Assert.AreEqual("exec-local", request.task_id);
            Assert.AreEqual(commandline, request.commandline);
            Assert.AreEqual("visible-to-test", request.spoofedcommandline);
            Assert.AreEqual(123, request.parent);
            Assert.IsFalse(request.output);
            Assert.IsFalse(request.suspended);

            TaskResponse response = messages.Responses.Single();
            Assert.AreEqual("Process Spawned", response.user_output);
            Assert.IsTrue(response.completed);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    private sealed class RecordingLocalProcessSpawner : ISpawner
    {
        public List<SpawnOptions> Requests { get; } = new();

        public async Task<bool> Spawn(SpawnOptions options)
        {
            Requests.Add(options);
            string[] parts = options.commandline.Split(' ', 2);
            using var process = Process.Start(new ProcessStartInfo(parts[0], parts.Length == 2 ? parts[1] : string.Empty)
            {
                UseShellExecute = false
            });
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        public bool TryGetHandle(string taskId, out SafeProcessHandle? handle)
        {
            handle = null;
            return false;
        }
    }
}
