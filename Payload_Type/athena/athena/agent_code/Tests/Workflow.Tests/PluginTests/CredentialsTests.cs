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
            var job = CreateJob("credentials", new { action = "dns-cache" });
            var response = await ExecuteAndGetResponse(job);
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
        public async Task Credentials_NotYetImplemented_ReturnsMessage()
        {
            var job = CreateJob("credentials", new { action = "dpapi" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            AssertOutputContains(response, "not yet implemented");
        }
    }
}
