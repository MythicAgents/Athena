using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class EventLogTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("event-log");
        }

        [TestMethod]
        public async Task EventLog_ListLogs_Windows()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
                return;
            }
            var job = CreateJob("event-log", new { action = "list" });
            var response = await ExecuteAndGetResponse(job);

            // EventLog.GetEventLogs() requires registry access; skip gracefully
            // when running without elevated privileges.
            if (response.status == "error" &&
                (response.user_output?.Contains("registry") == true ||
                 response.user_output?.Contains("privileges") == true ||
                 response.user_output?.Contains("access") == true))
            {
                Assert.Inconclusive("Insufficient privileges to list event logs (run as admin)");
                return;
            }

            AssertSuccess(response);
            AssertOutputContains(response, "Application");
        }

        [TestMethod]
        public async Task EventLog_QueryApplication_Windows()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
                return;
            }
            var job = CreateJob("event-log", new
            {
                action = "query",
                log_name = "Application",
                count = 5
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task EventLog_NonWindows_ReturnsError()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Non-Windows only");
                return;
            }
            var job = CreateJob("event-log", new { action = "list" });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
