using Agent.Interfaces;
using System.IO.Compression;
using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class ZipTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        IPlugin _zipPlugin { get; set; }
        public ZipTests()
        {
            _zipPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("zip");
        }

        [TestMethod]
        public async Task TestZipPlugin_FolderExists()
        {

            string sourcePath = Utilities.CreateTempDirectoryWithRandomFiles();
            string destinationPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".7z");
            Assert.IsTrue(Directory.Exists(sourcePath));


            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "source", sourcePath },
                { "destination",  destinationPath }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "zip"
                }
            };
            await _zipPlugin.Execute(job);
            string response = ((TestMessageManager)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(File.Exists(destinationPath));
            
            //Moves too fast, handles for temporary files don't get closed, so we gotta wait
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(destinationPath);

            //Test Unzip
            Directory.Delete(sourcePath,true);
        }
        [TestMethod]
        public async Task TestZipPlugin_FolderNotExists()
        {
            string sourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string destinationPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".7z");
            Assert.IsFalse(Directory.Exists(sourcePath));
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "source", sourcePath },
                { "destination",  destinationPath }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "zip"
                }
            };
            await _zipPlugin.Execute(job);
            string response = ((TestMessageManager)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(rr.status == "error" && rr.user_output.Contains("Source folder doesn't exist"));

        }
    }

    [TestClass]
    public class ZipDlTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        IPlugin _zipDlPlugin { get; set; }
        public ZipDlTests()
        {
            _zipDlPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("zip-dl");
        }

        [TestMethod]
        public async Task TestZipDlPlugin_FileExists()
        {

        }
        [TestMethod]
        public async Task TestZipDlPlugin_FileExistsWriteToDisk()
        {

        }
        [TestMethod]
        public async Task TestZipDlPlugin_FileExistsInMemory()
        {

        }
        [TestMethod]
        public async Task TestZipDlPlugin_FileNotExists()
        {
            var sourcePath = Path.GetTempPath() + Guid.NewGuid().ToString();
            Assert.IsFalse(Directory.Exists(sourcePath));
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "source", sourcePath },
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "zip-dl"
                }
            };
            await _zipDlPlugin.Execute(job);
            string response = ((TestMessageManager)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(rr.user_output.Contains("Directory doesn't exist"));
        }
    }

    [TestClass]
    public class ZipInspectTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        IPlugin _zipPlugin { get; set; }
        public ZipInspectTests()
        {
            _zipPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("zip-inspect");
        }

        [TestMethod]
        public async Task TestZipInspectPlugin_FileExists()
        {
            string sourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Assert.IsTrue(Utilities.CreateZipFile(sourcePath));
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", sourcePath },
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "zip-inspect"
                }
            };
            await _zipPlugin.Execute(job);
            string response = ((TestMessageManager)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            for(int i = 1; i < 6; i++)
            {
                Assert.IsTrue(rr.user_output.Contains("RandomFile_" + i));
            }

            //Moves too fast, handles for temporary files don't get closed, so we gotta wait
            GC.Collect();
            GC.WaitForPendingFinalizers();
            //Test Unzip
            File.Delete(sourcePath);
        }
        [TestMethod]
        public async Task TestZipInspectPlugin_FileNotExists()
        {
            var sourcePath = Path.GetTempPath() + Guid.NewGuid().ToString() + ".zip";
            Assert.IsFalse(Directory.Exists(sourcePath));
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", sourcePath },
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "zip-inspect"
                }
            };
            await _zipPlugin.Execute(job);
            string response = ((TestMessageManager)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(rr.user_output.Contains("Zipfile does not exist"));
        }
    }
}
