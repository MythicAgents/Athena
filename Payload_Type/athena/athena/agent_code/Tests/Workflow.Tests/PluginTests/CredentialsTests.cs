using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class CredentialsTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("credentials");
        }

        [TestMethod]
        public async Task Credentials_DnsCache_ReturnsOutput()
        {
            // dns-cache action was moved to the recon module
            var reconLoader = new PluginLoader(_messageManager);
            var reconPlugin = reconLoader.LoadPluginFromDisk("recon");
            Assert.IsNotNull(reconPlugin, "Failed to load plugin: recon");
            var job = CreateJob("recon", new { action = "dns-cache" });
            _ = Task.Run(() => reconPlugin.Execute(job));
            _messageManager.hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            string raw = _messageManager.GetRecentOutput();
            var response = System.Text.Json.JsonSerializer.Deserialize<TaskResponse>(raw);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "Should return some output");
        }

        [TestMethod]
        public async Task Credentials_ShadowRead_PlatformGated()
        {
            var job = CreateJob("credentials", new { action = "shadow-read" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            // On Windows: "Shadow file is only available on Linux"
            // On Linux without root: "Access denied"
            // On Linux with root: actual shadow contents
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "Should return platform-appropriate message");
        }

        [TestMethod]
        public async Task Credentials_UnknownAction_ReturnsError()
        {
            var job = CreateJob("credentials", new { action = "invalid-action" });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }

        [TestMethod]
        public async Task Credentials_Dpapi_ReturnsOutput()
        {
            var job = CreateJob("credentials", new { action = "dpapi" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "dpapi should return platform-appropriate output");
        }
    }
}
