using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class BitsTransferTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("bits-transfer");
        }

        [TestMethod]
        public async Task BitsTransfer_Download_ReturnsOutput()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
                return;
            }
            var job = CreateJob("bits-transfer", new
            {
                action = "download",
                url = "https://example.com/test.txt",
                path = @"C:\temp\test.txt"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task BitsTransfer_NonWindows_ReturnsError()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Non-Windows only");
                return;
            }
            var job = CreateJob("bits-transfer", new { action = "list" });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
