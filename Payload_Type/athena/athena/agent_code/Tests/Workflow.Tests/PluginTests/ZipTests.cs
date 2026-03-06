using Workflow.Contracts;
using System.IO.Compression;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class ZipTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        ISecurityProvider _cryptoManager = new TestCryptoManager();
        IDataBroker _messageManager = new TestDataBroker();
        IRuntimeExecutor _spawner = new TestSpawner();
        IModule _zipPlugin { get; set; }
        IModule _zipDlPlugin { get; set; }
        IModule _zipInspectPlugin { get; set; }
        public ZipTests()
        {
            _zipPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("zip");
            _zipDlPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("zip-dl");
            _zipInspectPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("zip-inspect");
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
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
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
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(rr.status == "error" && rr.user_output.Contains("Source folder doesn't exist"));

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
            Console.WriteLine("params");
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "source", sourcePath },
            };
            Console.WriteLine("job");
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "zip-dl"
                }
            };
            Console.WriteLine("exec");
            await _zipDlPlugin.Execute(job);
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            Console.WriteLine(response);
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(rr.user_output.Contains("Directory doesn't exist"));
        }
        [TestMethod]
        public async Task TestZipInspectPlugin_FileExists()
        {
            string sourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
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
            await _zipInspectPlugin.Execute(job);
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Console.WriteLine(response);
            for (int i = 1; i < 6; i++)
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
            await _zipInspectPlugin.Execute(job);
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.IsTrue(rr.user_output.Contains("Zipfile does not exist"));
        }
    }
}
