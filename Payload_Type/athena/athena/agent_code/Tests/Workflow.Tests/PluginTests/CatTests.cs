using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class CatTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        ISecurityProvider _cryptoManager = new TestCryptoManager();
        IDataBroker _messageManager = new TestDataBroker();
        IRuntimeExecutor _spawner = new TestSpawner();
        IModule _catPlugin { get; set; }
        public CatTests()
        {
            _catPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("cat");
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

            _ = Task.Run(() => _catPlugin.Execute(job));
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
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

            _ = Task.Run(() => _catPlugin.Execute(job));
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
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

            _ = Task.Run(() => _catPlugin.Execute(job));
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(String.IsNullOrEmpty(rr.user_output));
            File.Delete(tempFile);
        }
    }
}
