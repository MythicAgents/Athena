using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("FileOps")]
    public class FileUtilsTests : PluginTestBase
    {
        public FileUtilsTests()
        {
            LoadPlugin("file-utils");
        }

        [TestMethod]
        public async Task TestHead_FirstNLines()
        {
            string content = "line1\nline2\nline3\nline4\nline5\nline6";
            string tempFile = Utilities.CreateTempFileWithContent(content);

            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "head",
                    path = tempFile,
                    lines = 3
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("line1"));
            Assert.IsTrue(response.user_output.Contains("line3"));
            Assert.IsFalse(response.user_output.Contains("line4"));

            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task TestHead_FileNotFound()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "head",
                    path = "/tmp/nonexistent_file_xyz.txt"
                }));

            AssertError(response);
        }

        [TestMethod]
        public async Task TestWc_CountLines()
        {
            string content = "line1\nline2\nline3";
            string tempFile = Utilities.CreateTempFileWithContent(content);

            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "wc",
                    path = tempFile
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("3"));

            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task TestDiff_TwoFiles()
        {
            string file1 = Utilities.CreateTempFileWithContent("line1\nline2\nline3");
            string file2 = Utilities.CreateTempFileWithContent("line1\nmodified\nline3");

            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "diff",
                    path = file1,
                    path2 = file2
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("line2"));
            Assert.IsTrue(response.user_output.Contains("modified"));

            File.Delete(file1);
            File.Delete(file2);
        }

        [TestMethod]
        public async Task TestTouch_CreateFile()
        {
            string tempFile = Path.Combine(
                Path.GetTempPath(), $"touch_test_{Guid.NewGuid()}.txt");

            var response = await ExecuteAndGetResponse(
                CreateJob("file-utils", new
                {
                    action = "touch",
                    path = tempFile
                }));

            AssertSuccess(response);
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }
    }
}
