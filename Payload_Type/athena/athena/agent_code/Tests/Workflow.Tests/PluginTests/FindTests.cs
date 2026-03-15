using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("FileOps")]
    public class FindTests : PluginTestBase
    {
        public FindTests()
        {
            LoadPlugin("find");
        }

        [TestMethod]
        public async Task TestFind_ByPattern()
        {
            string tempDir = Utilities.CreateTempDirectoryWithStructure(
                new Dictionary<string, string>
                {
                    { "test.txt", "hello" },
                    { "test.log", "world" },
                    { "sub/nested.txt", "nested" }
                });

            var job = CreateJob("find", new
            {
                action = "find",
                path = tempDir,
                pattern = "*.txt",
                max_depth = 5
            });

            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("test.txt"));
            Assert.IsTrue(response.user_output.Contains("nested.txt"));
            Assert.IsFalse(response.user_output.Contains("test.log"));

            Directory.Delete(tempDir, true);
        }

        [TestMethod]
        public async Task TestFind_GrepContent()
        {
            string tempDir = Utilities.CreateTempDirectoryWithStructure(
                new Dictionary<string, string>
                {
                    { "match.txt", "password=secret123" },
                    { "nomatch.txt", "nothing here" }
                });

            var job = CreateJob("find", new
            {
                action = "grep",
                path = tempDir,
                content_pattern = "password",
                max_depth = 5
            });

            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("match.txt"));
            Assert.IsFalse(response.user_output.Contains("nomatch.txt"));

            Directory.Delete(tempDir, true);
        }

        [TestMethod]
        public async Task TestFind_InvalidPath()
        {
            var job = CreateJob("find", new
            {
                action = "find",
                path = "/nonexistent/path/abc123",
                pattern = "*"
            });

            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }

        [TestMethod]
        public async Task TestFind_MaxDepth()
        {
            string tempDir = Utilities.CreateTempDirectoryWithStructure(
                new Dictionary<string, string>
                {
                    { "level1.txt", "a" },
                    { "sub1/level2.txt", "b" },
                    { "sub1/sub2/level3.txt", "c" }
                });

            var job = CreateJob("find", new
            {
                action = "find",
                path = tempDir,
                pattern = "*.txt",
                max_depth = 1
            });

            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("level1.txt"));
            Assert.IsFalse(response.user_output.Contains("level3.txt"));

            Directory.Delete(tempDir, true);
        }
    }
}
