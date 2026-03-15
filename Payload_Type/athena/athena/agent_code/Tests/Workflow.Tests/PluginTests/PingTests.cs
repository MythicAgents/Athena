using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("Network")]
    public class PingTests : PluginTestBase
    {
        public PingTests()
        {
            LoadPlugin("ping");
        }

        [TestMethod]
        public async Task TestPing_Localhost()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("ping", new
                {
                    action = "ping",
                    host = "127.0.0.1",
                    count = 2,
                    timeout = 1000
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("Reply from"));
        }

        [TestMethod]
        public async Task TestPing_InvalidHost()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("ping", new
                {
                    action = "ping",
                    host = "192.0.2.1",
                    count = 1,
                    timeout = 500
                }));

            // Should complete (not crash), even if no reply
            Assert.IsNotNull(response);
            Assert.IsTrue(response.completed);
        }

        [TestMethod]
        public async Task TestTraceroute_Localhost()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("ping", new
                {
                    action = "traceroute",
                    host = "127.0.0.1",
                    max_ttl = 3,
                    timeout = 1000
                }));

            AssertSuccess(response);
            Assert.IsTrue(response.user_output.Contains("Traceroute"));
        }
    }
}
