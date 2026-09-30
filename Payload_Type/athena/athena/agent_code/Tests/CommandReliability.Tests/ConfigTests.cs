extern alias configplugin;

namespace CommandReliability.Tests;

[TestClass]
public sealed class ConfigTests
{
    [TestMethod]
    public async Task ConfigUpdatesAgentSettings()
    {
        var messages = new RecordingMessageManager();
        var config = new TestConfig();
        var plugin = new configplugin::Agent.Plugin(messages, config, null!, null!, null!, null!);

        await plugin.Execute(TestJobs.Create("config", new
        {
            sleep = 30,
            jitter = 15,
            chunk_size = 4096,
            prettyOutput = "true",
            debug = "true",
            inject = 2
        }));

        Assert.AreEqual(30, config.sleep);
        Assert.AreEqual(15, config.jitter);
        Assert.AreEqual(4096, config.chunk_size);
        Assert.IsTrue(config.prettyOutput);
        Assert.IsTrue(config.debug);
        Assert.AreEqual(2, config.inject);
        Assert.IsTrue(messages.Responses.Single().completed);
    }
}
