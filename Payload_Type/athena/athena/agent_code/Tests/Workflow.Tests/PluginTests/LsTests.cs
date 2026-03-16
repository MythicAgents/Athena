using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class LsTests
    {
        IDataBroker _messageManager = new TestDataBroker();
        IModule _plugin;
        ServerJob _job;

        public LsTests()
        {
            _plugin = new PluginLoader(_messageManager).LoadPluginFromDisk("ls");
            _job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "123",
                    command = "ls",
                    token = 0,
                }
            };
        }

        [TestMethod]
        public async Task TestValidParentPath()
        {
            string parentDir = Utilities.CreateTempDirectoryWithRandomFiles();
            string childDir = Path.Combine(parentDir, "subdir");
            Directory.CreateDirectory(childDir);
            File.WriteAllText(Path.Combine(childDir, "test.txt"), "hello");

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "path", childDir }
                };
                _job.task.parameters = JsonSerializer.Serialize(parameters);
                _ = Task.Run(() => _plugin.Execute(_job));

                ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
                string response = ((TestDataBroker)_messageManager).GetRecentOutput();
                var fb = JsonSerializer.Deserialize<FileBrowserTaskResponse>(response);

                Assert.IsNotNull(fb.file_browser);
                Assert.AreEqual(parentDir, fb.file_browser.parent_path);
            }
            finally
            {
                Directory.Delete(parentDir, true);
            }
        }

        [TestMethod]
        public async Task TestGetFileListingLocal()
        {
            string tempDir = Utilities.CreateTempDirectoryWithRandomFiles();

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "path", tempDir }
                };
                _job.task.parameters = JsonSerializer.Serialize(parameters);
                _ = Task.Run(() => _plugin.Execute(_job));

                ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
                string response = ((TestDataBroker)_messageManager).GetRecentOutput();
                var fb = JsonSerializer.Deserialize<FileBrowserTaskResponse>(response);

                Assert.IsNotNull(fb.file_browser);
                bool found = fb.file_browser.files.Any(f => f.name == "RandomFile_1.txt");
                Assert.IsTrue(found);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public async Task TestGetFileListing_Failure()
        {
            string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var parameters = new Dictionary<string, string>
            {
                { "path", fakePath }
            };
            _job.task.parameters = JsonSerializer.Serialize(parameters);
            _ = Task.Run(() => _plugin.Execute(_job));

            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            var rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.AreEqual("error", rr.status);
        }

        [TestMethod]
        public async Task TestPathParsingLocalFull()
        {
            string tempDir = Utilities.CreateTempDirectoryWithRandomFiles();

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "path", tempDir }
                };
                _job.task.parameters = JsonSerializer.Serialize(parameters);
                _ = Task.Run(() => _plugin.Execute(_job));

                ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
                string response = ((TestDataBroker)_messageManager).GetRecentOutput();
                var fb = JsonSerializer.Deserialize<FileBrowserTaskResponse>(response);

                Assert.IsNotNull(fb.file_browser);
                Assert.AreEqual(fb.file_browser.files.Count, 6);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public async Task TestPathParsingRelative()
        {
            string tempDir = Utilities.CreateTempDirectoryWithRandomFiles();
            string oldDir = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(tempDir);
                var parameters = new Dictionary<string, string>
                {
                    { "path", "." }
                };
                _job.task.parameters = JsonSerializer.Serialize(parameters);
                _ = Task.Run(() => _plugin.Execute(_job));

                ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
                string response = ((TestDataBroker)_messageManager).GetRecentOutput();
                var fb = JsonSerializer.Deserialize<FileBrowserTaskResponse>(response);

                Assert.IsNotNull(fb.file_browser);
                Assert.AreEqual(fb.file_browser.files.Count, 6);
            }
            finally
            {
                Directory.SetCurrentDirectory(oldDir);
                Directory.Delete(tempDir, true);
            }
        }
    }
}
