using Agent.Interfaces;
using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class CatTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        IPlugin _catPlugin { get; set; }
        public CatTests()
        {
            _catPlugin = PluginLoader.LoadPluginFromDisk("cat", _messageManager, _config, _logger, _tokenManager, _spawner);
        }
        [TestMethod]
        public async Task TestCatPlugin_FileExists()
        {
            string tempFile = Path.GetTempFileName();

            string stringToCompare = "I could not bring myself to fight my Father’s brother, Poseidon, quaking with anger at you, still enraged";

            File.WriteAllText(tempFile, stringToCompare);
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", tempFile }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "cat"
                }
            };

            await _catPlugin.Execute(job);
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(rr.user_output.Equals(stringToCompare));

            File.Delete(tempFile);
        }
        [TestMethod]
        public async Task TestCatPlugin_FileNotFound()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "Idontexistasdfewrwerw.txt");

            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", tempFile }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "cat"
                }
            };

            await _catPlugin.Execute(job);
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(rr.user_output.Contains("File does not exist"));
        }
        [TestMethod]
        public async Task TestCatPlugin_EmptyFile()
        {
            string tempFile = Path.GetTempFileName();

            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", tempFile }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "cat"
                }
            };

            await _catPlugin.Execute(job);
            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(String.IsNullOrEmpty(rr.user_output));
            File.Delete(tempFile);
        }
    }
}
