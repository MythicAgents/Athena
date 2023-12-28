using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class UploadTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        IPlugin _uploadPlugin { get; set; }
        ServerJob _uploadJob { get; set; }
        public UploadTests()
        {
            _uploadPlugin = PluginLoader.LoadPluginFromDisk("upload", _messageManager, _config, _logger, _tokenManager);
            _uploadJob = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "123",
                    command = "upload",
                    token = 0,
                }
            };
        }
        [TestMethod]
        public void TestPathParsingLocalFull()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            File.Create(fileName).Close();
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"remote_path", fileName },

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _uploadPlugin.Execute(_uploadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            UploadResponse ur = JsonSerializer.Deserialize<UploadResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            File.Delete(fileName);
            //Make sure
        }
        [TestMethod]
        public void TestPathParsingUnc()
        {
            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestPathParsingRelative()
        {
            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestSingleChunkUpload()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Utilities.CreateTemporaryFileWithRandomText(fileName, 256000);
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"remote_path", fileName },

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _uploadPlugin.Execute(_uploadJob);
            UploadResponse ur = JsonSerializer.Deserialize<UploadResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.IsTrue(ur is not null);

            Console.WriteLine(ur.upload.full_path);
            Console.WriteLine(ur.upload.chunk_num);
            Assert.AreEqual(fileName, ur.upload.full_path);

            File.Delete(fileName);
            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestMultiChunkUpload()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            //3 chunks
            Utilities.CreateTemporaryFileWithRandomText(fileName, 512000 * 3);
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"remote_path", fileName },

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);

            _uploadPlugin.Execute(_uploadJob);
            //Test to make sure the plugin parses local paths like we expect

            //Call HandleNextChunk

            //Get Response from MessageHandler

            //Call HandleNextChunk for the second time

            //Get Response from MessageHandler

            //Do I need to call it a third time?

            //Get Response from MessageHandler
            File.Delete(fileName);
        }
        [TestMethod]
        public void TestFileNotExist()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"remote_path", fileName },

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            //3 chunks
            //Test to make sure the plugin parses local paths like we expect

            File.Delete(fileName);
        }
    }
}
