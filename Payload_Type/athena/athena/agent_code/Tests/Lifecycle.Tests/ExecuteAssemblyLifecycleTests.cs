extern alias executeassembly;

using System.Text.Json;
using ExecuteAssemblyPlugin = executeassembly::Agent.Plugin;

namespace Lifecycle.Tests;

[TestClass]
[DoNotParallelize]
public class ExecuteAssemblyLifecycleTests
{
    [TestMethod]
    public async Task ExecuteAssemblyRunsAnUploadedConsoleApplication()
    {
        var messages = new RecordingMessageManager();
        var plugin = new ExecuteAssemblyPlugin(messages, null!, null!, null!, null!, null!);
        byte[] assembly = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "AsyncAssembly.dll"));

        await plugin.Execute(Jobs.Create("execute-assembly", JsonSerializer.Serialize(new
        {
            asm = Convert.ToBase64String(assembly),
            arguments = "smoke"
        })));

        TaskResponse[] responses = messages.Snapshot();
        Assert.IsTrue(responses.Any(response => response.user_output.Contains("before:")));
        Assert.IsTrue(responses.Any(response => response.user_output.Contains("smoke")));
        Assert.IsTrue(responses.Any(response => response.completed && response.user_output == "Assembly execution complete."));
    }
}
