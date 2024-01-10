using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class ConfigTest
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        IPlugin _configPlugin { get; set; }
        public ConfigTest()
        {
            _configPlugin = PluginLoader.LoadPluginFromDisk("config", _messageManager, _config, _logger, _tokenManager, null);
        }
        [TestMethod]
        public async Task TestSleepUpdate()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "sleep", 1000 },
                { "jitter", -1 },
                { "killdate", "01/01/0001" }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "config"
                }
            };

            await _configPlugin.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(_config.sleep == 1000);
        }
        [TestMethod]
        public async Task TestSleepInvalid()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "sleep", -1000 },
                { "jitter", -1000 },
                { "killdate", "01/01/0001" }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "config"
                }
            };

            await _configPlugin.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(_config.sleep == 10);
        }
        [TestMethod]
        public async Task TestJitterUpdate()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "sleep", 10 },
                { "jitter", 3000 },
                { "killdate", "01/01/0001" }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "config"
                }
            };

            await _configPlugin.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(_config.jitter == 3000);
        }
        [TestMethod]
        public async Task TestJitterInvalid()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "sleep", -1000 },
                { "jitter", -1000 },
                { "killdate", "01/01/0001" }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "config"
                }
            };

            await _configPlugin.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(_config.jitter == 10);
        }
        [TestMethod]
        public async Task TestKillDateUpdate()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "sleep", -1 },
                { "jitter", -1 },
                { "killdate", "10/10/2026" }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "config"
                }
            };

            await _configPlugin.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            Assert.IsTrue(_config.killDate == DateTime.Parse("10/10/2026"));
        }
        [TestMethod]
        public async Task TestKilldateInvalid()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "sleep", -1000 },
                { "jitter", -1000 },
                { "killdate", "" }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "config"
                }
            };

            await _configPlugin.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(_config.killDate.Date == DateTime.Now.AddYears(1).Date);
        }
    }
}
