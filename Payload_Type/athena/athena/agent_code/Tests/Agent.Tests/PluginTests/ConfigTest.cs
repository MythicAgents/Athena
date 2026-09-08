using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class ConfigTests
    {
        [TestMethod]
        public async Task ConfigCommandUpdatesAgentSettings()
        {
            var config = new TestAgentConfig();
            IMessageManager messages = new TestMessageManager();
            var loader = new PluginLoader(messages) { agentConfig = config };
            IPlugin plugin = loader.LoadPluginFromDisk("config");
            DateTime killDate = new(2026, 10, 10);
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "config-smoke",
                    command = "config",
                    parameters = JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        ["sleep"] = 1000,
                        ["jitter"] = 3000,
                        ["killdate"] = killDate.ToString("MM/dd/yyyy")
                    })
                }
            };

            await plugin.Execute(job);

            Assert.AreEqual(1000, config.sleep);
            Assert.AreEqual(3000, config.jitter);
            Assert.AreEqual(killDate, config.killDate.Date);
            TaskResponse response = JsonSerializer.Deserialize<TaskResponse>(((TestMessageManager)messages).GetRecentOutput())!;
            Assert.IsFalse(string.Equals("error", response.status, StringComparison.OrdinalIgnoreCase));
        }
    }
}
