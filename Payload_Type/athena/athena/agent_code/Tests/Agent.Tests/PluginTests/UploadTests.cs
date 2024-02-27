using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Agent.Utilities;
using System.Security.Cryptography;

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
        ISpawner _spawner = new TestSpawner();
        IFilePlugin _uploadPlugin { get; set; }
        ServerJob _uploadJob { get; set; }
        public UploadTests()
        {
            _uploadPlugin = (IFilePlugin)PluginLoader.LoadPluginFromDisk("upload", _messageManager, _config, _logger, _tokenManager, _spawner);
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
            //Assert.IsTrue(false);
            string directory = Path.GetTempPath();
            string fileName = Guid.NewGuid().ToString() + ".txt";
            string fullPath = Path.Combine(directory, fileName);
            File.Create(fullPath).Close();
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"path", directory },
                {"filename", fileName },
                {"host", Environment.MachineName },

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            Console.WriteLine(_uploadJob.task.parameters);
            _uploadPlugin.Execute(_uploadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            UploadTaskResponse ur = JsonSerializer.Deserialize<UploadTaskResponse>(response);


            //Make sure
            Assert.AreEqual(ur.upload.full_path, fullPath);
        }
        [TestMethod]
        public void TestPathParsingRelative()
        {
            //Assert.IsTrue(false);
            string fileName = Guid.NewGuid().ToString() + ".txt";
            File.Create(fileName).Close();
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"path",  ""},
                {"filename","myfile.txt" }

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _uploadPlugin.Execute(_uploadJob);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            UploadTaskResponse ur = JsonSerializer.Deserialize<UploadTaskResponse>(response);


            //Make sure
            Assert.AreEqual(ur.upload.full_path, Path.Combine(Directory.GetCurrentDirectory(),"myfile.txt"));
            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestSingleChunkUpload()
        {
            string fileName = Guid.NewGuid().ToString() + ".txt";
            string pathName = Path.GetTempPath();
            string fullPath = Path.Combine(pathName, fileName);
            Utilities.CreateTemporaryFileWithRandomText(fullPath, 256000);
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"path", pathName },
                {"filename", fileName },
                {"host", Environment.MachineName }

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            this._config.chunk_size = 256000;
            _uploadPlugin.Execute(_uploadJob);
            UploadTaskResponse ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.IsTrue(ur is not null);

            Console.WriteLine(ur.upload.full_path);
            Console.WriteLine(ur.upload.chunk_num);
            Assert.AreEqual(fullPath, ur.upload.full_path);

            File.Delete(fileName);
            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestMultiChunkUpload()
        {
            string fileName = Guid.NewGuid().ToString() + ".txt";
            string pathName = Path.GetTempPath();
            string fullPath = Path.Combine(pathName, fileName);
            string fullPath2 = fullPath + "2";
            //3 chunks
            Utilities.CreateTemporaryFileWithRandomText(fullPath, (512000 * 3) + 1000);
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"path", pathName },
                {"filename", fileName + "2" },
                {"host", Environment.MachineName }

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _config.chunk_size = 512000;
            _uploadPlugin.Execute(_uploadJob);
            UploadTaskResponse ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);
            Assert.IsTrue(ur is not null);
            Assert.AreEqual(fullPath2, ur.upload.full_path);
            //Test to make sure the plugin parses local paths like we expect

            //Call HandleNextChunk
            ServerTaskingResponse responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = 4,
                chunk_data = this.TryHandleNextChunk(fullPath, 1),
                chunk_num = 1,
            };
            _uploadPlugin.HandleNextMessage(responseResult);
            ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            FileInfo fileAttribs = new FileInfo(fullPath2);
            Console.WriteLine(fileAttribs.Length);
            Assert.AreEqual(fileAttribs.Length, 512000);

            responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = 3,
                chunk_data = this.TryHandleNextChunk(fullPath, 2),
                chunk_num = 2,
            };
            _uploadPlugin.HandleNextMessage(responseResult);
            ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            fileAttribs = new FileInfo(fullPath2);
            Console.WriteLine(fileAttribs.Length);
            Assert.AreEqual(fileAttribs.Length, 512000 * 2);

            responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = 3,
                chunk_data = this.TryHandleNextChunk(fullPath, 3),
                chunk_num = 3,
            };
            _uploadPlugin.HandleNextMessage(responseResult);
            ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            fileAttribs = new FileInfo(fullPath2);
            Console.WriteLine(fileAttribs.Length);
            Assert.AreEqual(fileAttribs.Length, 512000*3);

            responseResult = new ServerTaskingResponse()
            {
                task_id = "123",
                file_id = "1234",
                total_chunks = 3,
                chunk_data = this.TryHandleNextChunk(fullPath, 4),
                chunk_num = 4,
            };
            _uploadPlugin.HandleNextMessage(responseResult);
            ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            fileAttribs = new FileInfo(fullPath2);
            Console.WriteLine(fileAttribs.Length);
            Assert.AreEqual(fileAttribs.Length, (512000 * 3)+1000);
            Assert.AreEqual(GetHashForFile(fullPath), GetHashForFile(fullPath2));
            File.Delete(fullPath);
            File.Delete(fullPath2);
        }
        [TestMethod]
        public void TestFileNotExist()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                {"path", fileName },

            };
            _uploadJob.task.parameters = JsonSerializer.Serialize(downloadParams);
            _uploadPlugin.Execute(_uploadJob);
            UploadTaskResponse ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput().Result);

            Assert.IsTrue(ur.status == "error");
        }

        public string TryHandleNextChunk(string path, int chunk)
        {
            try
            {
                long totalBytesRead = 512000 * (chunk - 1);

                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[512000];

                    FileInfo fileInfo = new FileInfo(path);

                    if (fileInfo.Length - totalBytesRead < 512000)
                    {
                        buffer = new byte[fileInfo.Length - (512000 * (chunk-1))];
                    }

                    fileStream.Seek((512000 * (chunk - 1)), SeekOrigin.Begin);
                    fileStream.Read(buffer, 0, buffer.Length);
                    return Misc.Base64Encode(buffer);
                };
            }
            catch (Exception e)
            {
                return "";
            }
        }

        static string GetHashForFile(string filename)
        {
            using (var md5 = MD5.Create())
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
    }
}
