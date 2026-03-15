using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class ClipboardMonitorTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("clipboard-monitor");
        }

        [TestMethod]
        public async Task ClipboardMonitor_NonWindows_ReturnsError()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Test for non-Windows only");
                return;
            }
            var job = CreateJob("clipboard-monitor", new
            {
                duration = 1,
                interval = 1
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }

        [TestMethod]
        public async Task ClipboardMonitor_ShortDuration_Completes()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
                return;
            }
            var job = CreateJob("clipboard-monitor", new
            {
                duration = 1,
                interval = 1
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }
    }
}
