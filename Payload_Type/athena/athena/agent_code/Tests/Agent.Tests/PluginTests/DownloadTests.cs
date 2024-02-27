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
            string directory = Path.GetTempPath();
            string fileName = Guid.NewGuid().ToString() + ".txt";
            string fullPath = Path.Combine(directory, fileName);
            Utilities.CreateTemporaryFileWithRandomText(Path.Combine(directory, fileName), 512000 * 3);
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"host", Dns.GetHostName() },
                {"path", fullPath },

            };
            _downloadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _downloadPlugin.Execute(_downloadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(response);

            Assert.AreEqual(ur.download.full_path, Path.Combine(Path.GetTempPath(), fileName));
        }
        [TestMethod]
        public void TestPathParsingRelative()
        {
            string fileName = Guid.NewGuid().ToString() + ".txt";
            string directory = Path.GetTempPath();
            Utilities.CreateTemporaryFileWithRandomText(Path.Combine(directory, fileName), 512000 * 3);
            string hostName = Dns.GetHostName();

            string directory_old = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetTempPath());
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"host", hostName },
                {"path", fileName },

            };
            _downloadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _downloadPlugin.Execute(_downloadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.AreNotEqual(ur.status, "error");
            Directory.SetCurrentDirectory(directory_old);
        }
        [TestMethod]
        public void TestMultiChunkDownload()
        {
            List<byte> fileBytes = new List<byte>();
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Utilities.CreateTemporaryFileWithRandomText(fileName, 512000 * 3);
            _config.chunk_size = 512000;
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "host", Dns.GetHostName() },
                { "path", fileName },

            };
            _downloadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _downloadPlugin.Execute(_downloadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.AreNotEqual(ur.status, "error");
            ServerTaskingResponse responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = ur.download.total_chunks,
                chunk_num = ur.download.chunk_num,

                status = "success"
            };
            _downloadPlugin.HandleNextMessage(responseResult);
            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.IsNotNull(ur.download.chunk_data);
            byte[] buf = Misc.Base64DecodeToByteArray(ur.download.chunk_data);
            fileBytes.AddRange(buf);

            responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                status = "success"
            };
            _downloadPlugin.HandleNextMessage(responseResult);
            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);
            Assert.IsNotNull(ur.download.chunk_data);
            buf = Misc.Base64DecodeToByteArray(ur.download.chunk_data);
            fileBytes.AddRange(buf);

            responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                status = "success"
            };
            _downloadPlugin.HandleNextMessage(responseResult);
            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);
            Assert.IsNotNull(ur.download.chunk_data);
            buf = Misc.Base64DecodeToByteArray(ur.download.chunk_data);
            fileBytes.AddRange(buf);

            Assert.AreEqual(GetHashForFile(fileName), GetHashForByteArray(fileBytes.ToArray()));
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
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.AreNotEqual(ur.status, "error");
            ServerTaskingResponse responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = ur.download.total_chunks,
                chunk_num = ur.download.chunk_num,

                status = "success"
            };
            _downloadPlugin.HandleNextMessage(responseResult);
            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

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
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);
            ServerTaskingResponse responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = 4,
                chunk_num = 1,
                status = "failed"
            };

            _downloadPlugin.HandleNextMessage(responseResult);
            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.AreEqual(rr.user_output, "An error occurred while communicating with the server." + Environment.NewLine);
        }

        [TestMethod]
        public void TestUncPathParsing()
        {

            if (!OperatingSystem.IsWindows())
            {
                Assert.IsTrue(true);
                return;
            }

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
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.AreEqual(ur.download.full_path, "\\\\127.0.0.1\\C$\\Windows\\System32\\drivers\\etc\\hosts");
        }

        [TestMethod]
        public void TestPathParsingUncWithFile()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.IsTrue(true);
                return;
            }
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
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

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
