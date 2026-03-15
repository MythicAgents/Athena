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
            LoadPlugin("env");
            var job = CreateJob("env", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "env should return environment variables");
        }

        [TestMethod]
        public async Task Hostname_ReturnsOutput()
        {
            LoadPlugin("hostname");
            var job = CreateJob("hostname", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "hostname should return a value");
        }

        [TestMethod]
        public async Task Whoami_ReturnsOutput()
        {
            LoadPlugin("whoami");
            var job = CreateJob("whoami", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "whoami should return current user");
        }

        [TestMethod]
        public async Task Uptime_ReturnsOutput()
        {
            LoadPlugin("uptime");
            var job = CreateJob("uptime", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "uptime should return system uptime");
        }

        [TestMethod]
        public async Task Drives_ReturnsOutput()
        {
            LoadPlugin("drives");
            var job = CreateJob("drives", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "drives should return drive info");
        }

        [TestMethod]
        public async Task Ps_ReturnsOutput()
        {
            LoadPlugin("ps");
            var job = CreateJob("ps", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task Nslookup_ReturnsOutput()
        {
            LoadPlugin("nslookup");
            var job = CreateJob("nslookup", new
            {
                hosts = "localhost"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task Arp_ReturnsOutput()
        {
            LoadPlugin("arp");
            var job = CreateJob("arp", new
            {
                cidr = "127.0.0.1/32",
                timeout = "1"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task Netstat_ReturnsOutput()
        {
            LoadPlugin("netstat");
            var job = CreateJob("netstat", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task Ifconfig_ReturnsOutput()
        {
            LoadPlugin("ifconfig");
            var job = CreateJob("ifconfig", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }
    }
}
