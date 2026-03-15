using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("SysInfo")]
    public class SysinfoTests : PluginTestBase
    {
        public SysinfoTests()
        {
            LoadPlugin("sysinfo");
        }

        [TestMethod]
        public async Task TestSysinfo_BasicInfo()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("sysinfo", new { action = "sysinfo" }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("Hostname:"));
            Assert.IsTrue(response.user_output.Contains("Username:"));
            Assert.IsTrue(response.user_output.Contains("OS:"));
        }

        [TestMethod]
        public async Task TestSysinfo_Id()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("sysinfo", new { action = "id" }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("Username:"));
        }

        [TestMethod]
        public async Task TestSysinfo_ContainerDetect()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("sysinfo", new { action = "container-detect" }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Length > 0);
        }

        [TestMethod]
        public async Task TestSysinfo_DotnetVersions()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("sysinfo", new { action = "dotnet-versions" }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Length > 0);
        }

        [TestMethod]
        public async Task TestSysinfo_UnknownAction()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("sysinfo", new { action = "invalid-action" }));

            AssertError(response);
        }
    }
}
