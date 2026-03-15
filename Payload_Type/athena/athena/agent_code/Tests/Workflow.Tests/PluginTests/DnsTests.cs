using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class DnsTests : PluginTestBase
    {
        public DnsTests()
        {
            LoadPlugin("dns");
        }

        [TestMethod]
        public async Task TestDns_LoadsSuccessfully()
        {
            Assert.IsNotNull(_plugin);
            Assert.AreEqual("dns", _plugin.Name);
        }

        [TestMethod]
        public async Task TestDns_EmptyHostname_ReturnsError()
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
        public async Task TestDns_NullArgs_ReturnsError()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("dns", new { }));

            AssertError(response);
        }
    }
}
