using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("FileOps")]
    public class HashTests : PluginTestBase
    {
        public HashTests()
        {
            LoadPlugin("hash");
        }

        [TestMethod]
        public async Task TestHash_Sha256()
        {
            string tempFile = Utilities.CreateTempFileWithContent("test content");

            try
            {
                var response = await ExecuteAndGetResponse(CreateJob("hash", new
                {
                    action = "hash",
                    path = tempFile,
                    algorithm = "sha256"
                }));

                AssertSuccess(response);
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
            var response = await ExecuteAndGetResponse(CreateJob("hash", new
            {
                action = "hash",
                path = "/nonexistent/file.txt",
                algorithm = "sha256"
            }));

            AssertError(response);
        }

        [TestMethod]
        public async Task TestBase64_Encode()
        {
            string tempFile = Utilities.CreateTempFileWithContent("hello world");

            try
            {
                var response = await ExecuteAndGetResponse(CreateJob("hash", new
                {
                    action = "base64",
                    path = tempFile,
                    encode = true
                }));

                AssertSuccess(response);
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
                var response = await ExecuteAndGetResponse(CreateJob("hash", new
                {
                    action = "base64",
                    path = tempFile,
                    encode = false
                }));

                AssertSuccess(response);
                Assert.AreEqual("hello world", response.user_output.Trim());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
