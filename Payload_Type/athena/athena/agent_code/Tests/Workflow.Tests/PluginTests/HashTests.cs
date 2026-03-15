using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("FileOps")]
    public class HashTests
    {
        IDataBroker _messageManager = new TestDataBroker();
        IModule _plugin;

        public HashTests()
        {
            _plugin = new PluginLoader(_messageManager).LoadPluginFromDisk("hash");
        }

        private ServerJob CreateJob(object parameters)
        {
            return new ServerJob()
            {
                task = new ServerTask()
                {
                    id = Guid.NewGuid().ToString(),
                    command = "hash",
                    token = 0,
                    parameters = JsonSerializer.Serialize(parameters),
                }
            };
        }

        private async Task<TaskResponse> ExecuteAndGetResponse(ServerJob job)
        {
            await _plugin.Execute(job);
            ((TestDataBroker)_messageManager).hasResponse.WaitOne();
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            return JsonSerializer.Deserialize<TaskResponse>(response);
        }

        [TestMethod]
        public async Task TestHash_Sha256()
        {
            string tempFile = Utilities.CreateTempFileWithContent("test content");

            try
            {
                var response = await ExecuteAndGetResponse(CreateJob(new
                {
                    action = "hash",
                    path = tempFile,
                    algorithm = "sha256"
                }));

                Assert.AreNotEqual("error", response.status);
                Assert.AreEqual(64, response.user_output.Trim().Length);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task TestHash_FileNotFound()
        {
            var response = await ExecuteAndGetResponse(CreateJob(new
            {
                action = "hash",
                path = "/nonexistent/file.txt",
                algorithm = "sha256"
            }));

            Assert.AreEqual("error", response.status);
        }

        [TestMethod]
        public async Task TestBase64_Encode()
        {
            string tempFile = Utilities.CreateTempFileWithContent("hello world");

            try
            {
                var response = await ExecuteAndGetResponse(CreateJob(new
                {
                    action = "base64",
                    path = tempFile,
                    encode = true
                }));

                Assert.AreNotEqual("error", response.status);
                Assert.AreEqual("aGVsbG8gd29ybGQ=", response.user_output.Trim());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task TestBase64_Decode()
        {
            string tempFile = Utilities.CreateTempFileWithContent("aGVsbG8gd29ybGQ=");

            try
            {
                var response = await ExecuteAndGetResponse(CreateJob(new
                {
                    action = "base64",
                    path = tempFile,
                    encode = false
                }));

                Assert.AreNotEqual("error", response.status);
                Assert.AreEqual("hello world", response.user_output.Trim());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
