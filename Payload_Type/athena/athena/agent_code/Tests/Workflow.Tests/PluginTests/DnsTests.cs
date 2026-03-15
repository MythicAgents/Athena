using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("Network")]
    public class DnsTests : PluginTestBase
    {
        public DnsTests()
        {
            LoadPlugin("dns");
        }

        [TestMethod]
        public async Task TestDns_LookupLocalhost()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("dns", new
                {
                    hostname = "localhost",
                    record_type = "A"
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("127.0.0.1") || response.user_output.Contains("::1"));
        }

        [TestMethod]
        public async Task TestDns_EmptyHostname()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("dns", new
                {
                    hostname = "",
                    record_type = "A"
                }));

            AssertError(response);
        }

        [TestMethod]
        public async Task TestDns_InvalidHostname()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("dns", new
                {
                    hostname = "this.host.definitely.does.not.exist.invalid",
                    record_type = "A"
                }));

            AssertError(response);
        }
    }
}
