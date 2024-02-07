using Agent.Utilities;
using SshNet.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class DownloadTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        IFilePlugin _downloadPlugin { get; set; }
        ServerJob _downloadJob { get; set; }
        public DownloadTests()
        {
            _downloadPlugin = (IFilePlugin)PluginLoader.LoadPluginFromDisk("download", _messageManager, _config, _logger, _tokenManager, _spawner);
            _downloadJob = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "123",
                    command = "download",
                    token = 0,
                    parameters = "",

                }
            };
        }
        [TestMethod]
        public void TestPathParsingLocalFull()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "path", fileName },

            };

            Assert.IsTrue(false);
            //Test to make sure the plugin parses local paths like we expect
        }
        public void TestPathParsingUnc()
        {
            Assert.IsTrue(false);

            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestPathParsingRelative()
        {
            Assert.IsTrue(false);

            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestMultiChunkDownload()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Utilities.CreateTemporaryFileWithRandomText(fileName, 256000);
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "path", fileName },

            };
            Assert.IsTrue(false);

            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestSingleChunkDownload()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Utilities.CreateTemporaryFileWithRandomText(fileName, 512000);
            _config.chunk_size = 512000;
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "host", Dns.GetHostName() },
                { "path", fileName },

            };
            _downloadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _downloadPlugin.Execute(_downloadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            DownloadResponse ur = JsonSerializer.Deserialize<DownloadResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);
            ServerResponseResult responseResult = new ServerResponseResult()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = 4,
                chunk_num = 1,
                status = "success"
            };
            _downloadPlugin.HandleNextMessage(responseResult);
            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            ur = JsonSerializer.Deserialize<DownloadResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.IsNotNull(ur.download.chunk_data);
            Assert.AreNotEqual(Misc.Base64DecodeToByteArray(ur.download.chunk_data).Length, 0);
            Assert.AreEqual(GetHashForByteArray(Misc.Base64DecodeToByteArray(ur.download.chunk_data)), GetHashForFile(fileName));
        }
        [TestMethod]
        public void TestHandleNextChunkFailure()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Utilities.CreateTemporaryFileWithRandomText(fileName, 512000);
            _config.chunk_size = 512000;
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "host", Dns.GetHostName() },
                { "path", fileName },

            };
            _downloadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _downloadPlugin.Execute(_downloadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            DownloadResponse ur = JsonSerializer.Deserialize<DownloadResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);
            ServerResponseResult responseResult = new ServerResponseResult()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = 4,
                chunk_num = 1,
                status = "failed"
            };

            _downloadPlugin.HandleNextMessage(responseResult);
            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            ur = JsonSerializer.Deserialize<DownloadResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.AreEqual(ur.status, "error");
            //Test to make sure the plugin parses local paths like we expect
        }

        [TestMethod]
        public void TestUncPathParsing()
        {
            string hostName = "127.0.0.1";
            string filePath = "C$\\Windows\\System32\\drivers\\etc\\hosts";
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"host", hostName },
                {"path", filePath },

            };
            _downloadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _downloadPlugin.Execute(_downloadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            DownloadResponse ur = JsonSerializer.Deserialize<DownloadResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.AreEqual(ur.download.full_path, "\\\\127.0.0.1\\C$\\Windows\\System32\\drivers\\etc\\hosts");
        }

        [TestMethod]
        public void TestPathParsingUncWithFile()
        {
            string hostName = "127.0.0.1";
            string filePath = "C$\\Windows\\System32\\drivers\\etc";
            string fileName = "hosts";
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "path", filePath },
                { "file", fileName},
                { "host", hostName }

            };

            _downloadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _downloadPlugin.Execute(_downloadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            DownloadResponse ur = JsonSerializer.Deserialize<DownloadResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.AreEqual(ur.download.full_path, "\\\\127.0.0.1\\C$\\Windows\\System32\\drivers\\etc\\hosts");
        }
        string GetHashForFile(string filename)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    var strHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    Console.WriteLine(strHash);
                    return strHash;
                }


            }
        }
        string GetHashForByteArray(byte[] bytes)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = new MemoryStream(bytes))
                {
                    var hash = md5.ComputeHash(stream);
                    var strHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    Console.WriteLine(strHash);
                    return strHash;
                }
            }
        }
    }
}
