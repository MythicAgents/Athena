extern alias executemoduleplugin;

namespace CommandReliability.Tests;

[TestClass]
public sealed class ExecuteModuleTests
{
    [TestMethod]
    public async Task UploadedModuleCanBeExecuted()
    {
        var messages = new RecordingMessageManager();
        var plugin = new executemoduleplugin::Agent.Plugin(messages, new TestConfig(), null!, null!, null!, null!);
        byte[] assembly = File.ReadAllBytes(typeof(ReloadOne.Entry).Assembly.Location);

        await plugin.Execute(TestJobs.Create("load", new
        {
            file = "fixture",
            name = "smoke-module",
            entrypoint = "Run",
            arguments = ""
        }));
        await plugin.HandleNextMessage(new ServerTaskingResponse
        {
            task_id = "load",
            total_chunks = 1,
            chunk_num = 1,
            chunk_data = Convert.ToBase64String(assembly)
        });
        await plugin.Execute(TestJobs.Create("execute", new
        {
            file = "",
            name = "smoke-module",
            entrypoint = "Run",
            arguments = ""
        }));

        Assert.AreEqual("version-one", messages.Responses.Single(response => response.task_id == "execute").user_output);
    }
}
