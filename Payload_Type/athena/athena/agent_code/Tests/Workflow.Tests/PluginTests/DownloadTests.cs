using Workflow.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class DownloadTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        ISecurityProvider _cryptoManager = new TestCryptoManager();
        IDataBroker _messageManager = new TestDataBroker();
        IRuntimeExecutor _spawner = new TestSpawner();
        IFileModule _downloadPlugin { get; set; }
        ServerJob _downloadJob { get; set; }
        public DownloadTests()
        {
            PluginLoader loader = new PluginLoader(_messageManager);
            _downloadPlugin = (IFileModule)loader.LoadPluginFromDisk("download");
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

            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
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

            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());

            Assert.AreNotEqual(ur.status, "error");
            Directory.SetCurrentDirectory(directory_old);
        }
        [TestMethod]
        public async Task TestMultiChunkDownload()
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
            _ = Task.Run(() => _downloadPlugin.Execute(_downloadJob));

            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());

            Assert.AreNotEqual(ur.status, "error");
            ServerTaskingResponse responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = ur.download.total_chunks,
                chunk_num = ur.download.chunk_num,

                status = "success"
            };
            await _downloadPlugin.HandleNextMessage(responseResult);
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());

            Assert.IsNotNull(ur.download.chunk_data);
            byte[] buf = Misc.Base64DecodeToByteArray(ur.download.chunk_data);
            fileBytes.AddRange(buf);

            responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                status = "success"
            };
            await _downloadPlugin.HandleNextMessage(responseResult);
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());
            Assert.IsNotNull(ur.download.chunk_data);
            buf = Misc.Base64DecodeToByteArray(ur.download.chunk_data);
            fileBytes.AddRange(buf);

            responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                status = "success"
            };
            await _downloadPlugin.HandleNextMessage(responseResult);
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());
            Assert.IsNotNull(ur.download.chunk_data);
            buf = Misc.Base64DecodeToByteArray(ur.download.chunk_data);
            fileBytes.AddRange(buf);

            Assert.AreEqual(GetHashForFile(fileName), GetHashForByteArray(fileBytes.ToArray()));
        }
        [TestMethod]
        public async Task TestSingleChunkDownload()
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
            _ = Task.Run(() => _downloadPlugin.Execute(_downloadJob));

            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());

            Assert.AreNotEqual(ur.status, "error");
            ServerTaskingResponse responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = ur.download.total_chunks,
                chunk_num = ur.download.chunk_num,

                status = "success"
            };
            await _downloadPlugin.HandleNextMessage(responseResult);
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());

            Assert.IsNotNull(ur.download.chunk_data);
            Assert.AreNotEqual(Misc.Base64DecodeToByteArray(ur.download.chunk_data).Length, 0);
            Console.WriteLine(Misc.Base64Decode(ur.download.chunk_data));
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

            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());
            ServerTaskingResponse responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = 4,
                chunk_num = 1,
                status = "failed"
            };

            _downloadPlugin.HandleNextMessage(responseResult);
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(response);
            Assert.AreEqual(rr.user_output, "An error occurred while communicating with the server.");
        }

        [TestMethod]
        public void TestUncPathParsing()
        {

            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Test requires Windows");
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

            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());

            Assert.AreEqual(ur.download.full_path, "\\\\127.0.0.1\\C$\\Windows\\System32\\drivers\\etc\\hosts");
        }

        [TestMethod]
        public void TestPathParsingUncWithFile()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Test requires Windows");
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

            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            DownloadTaskResponse ur = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());

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
