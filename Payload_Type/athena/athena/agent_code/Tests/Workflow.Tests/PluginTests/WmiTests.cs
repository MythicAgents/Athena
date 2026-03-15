using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class WmiTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("wmi");
        }

        [TestMethod]
        public async Task Wmi_Query_Win32OS()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
                return;
            }
            var job = CreateJob("wmi", new
            {
                action = "query",
                query = "SELECT Caption FROM Win32_OperatingSystem"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            AssertOutputContains(response, "Windows");
        }

        [TestMethod]
        public async Task Wmi_NonWindows_ReturnsError()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Non-Windows only");
                return;
            }
            var job = CreateJob("wmi", new
            {
                action = "query",
                query = "SELECT * FROM Win32_Process"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }

        [TestMethod]
        public async Task Wmi_EmptyQuery_ReturnsError()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
                return;
            }
            var job = CreateJob("wmi", new
            {
                action = "query",
                query = ""
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
