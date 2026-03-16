using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("Smoke")]
    public class ExistingPluginSmokeTests : PluginTestBase
    {
        [TestMethod]
        public async Task Env_ReturnsOutput()
        {
            LoadPlugin("sysinfo");
            var job = CreateJob("sysinfo", new { action = "env" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "env should return environment variables");
        }

        [TestMethod]
        public async Task Hostname_ReturnsOutput()
        {
            LoadPlugin("sysinfo");
            var job = CreateJob("sysinfo", new { action = "hostname" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "hostname should return a value");
        }

        [TestMethod]
        public async Task Whoami_ReturnsOutput()
        {
            LoadPlugin("sysinfo");
            var job = CreateJob("sysinfo", new { action = "whoami" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "whoami should return current user");
        }

        [TestMethod]
        public async Task Uptime_ReturnsOutput()
        {
            LoadPlugin("sysinfo");
            var job = CreateJob("sysinfo", new { action = "uptime" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "uptime should return system uptime");
        }

        [TestMethod]
        public async Task Drives_ReturnsOutput()
        {
            LoadPlugin("sysinfo");
            var job = CreateJob("sysinfo", new { action = "drives" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "drives should return drive info");
        }

        [TestMethod]
        public async Task Ps_ReturnsOutput()
        {
            LoadPlugin("proc-enum");
            var job = CreateJob("proc-enum", new { action = "ps" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task Nslookup_LoadsSuccessfully()
        {
            LoadPlugin("dns");
            Assert.IsNotNull(_plugin);
            Assert.AreEqual("dns", _plugin.Name);
        }

        [TestMethod]
        public async Task Arp_LoadsSuccessfully()
        {
            LoadPlugin("net-enum");
            Assert.IsNotNull(_plugin);
            Assert.AreEqual("net-enum", _plugin.Name);
        }

        [TestMethod]
        public async Task Netstat_LoadsSuccessfully()
        {
            LoadPlugin("net-enum");
            Assert.IsNotNull(_plugin);
            Assert.AreEqual("net-enum", _plugin.Name);
        }

        [TestMethod]
        public async Task Ifconfig_ReturnsOutput()
        {
            LoadPlugin("net-enum");
            var job = CreateJob("net-enum", new { action = "ifconfig" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task Reg_Query_ReturnsOutput()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation
                .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
            }
            LoadPlugin("reg");
            var job = CreateJob("reg", new
            {
                action = "query",
                hostName = "",
                keyPath = @"HKCU\SOFTWARE\Microsoft"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task Timestomp_LoadsAndRuns()
        {
            LoadPlugin("file-utils");
            var job = CreateJob("file-utils", new
            {
                action = "timestomp",
                source = "",
                destination = ""
            });
            var response = await ExecuteAndGetResponse(job);
            Assert.IsNotNull(response, "timestomp should produce a response");
        }

        [TestMethod]
        public async Task Config_ReturnsOutput()
        {
            LoadPlugin("config");
            var job = CreateJob("config", new
            {
                sleep = -1,
                jitter = -1
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task Pwd_ReturnsOutput()
        {
            LoadPlugin("pwd");
            var job = CreateJob("pwd", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "pwd should return current directory");
        }

        [TestMethod]
        public async Task Zip_MissingArgs_ReturnsError()
        {
            LoadPlugin("zip");
            var job = CreateJob("zip", new
            {
                source = "",
                destination = ""
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
