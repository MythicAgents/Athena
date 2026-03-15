using Workflow.Contracts;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class PingTests : PluginTestBase
    {
        public PingTests()
        {
            LoadPlugin("ping");
        }

        [TestMethod]
        public async Task TestPing_LoadsSuccessfully()
        {
            Assert.IsNotNull(_plugin);
            Assert.AreEqual("ping", _plugin.Name);
        }

        [TestMethod]
        public async Task TestPing_EmptyHost_ReturnsError()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("ping", new
                {
                    action = "ping",
                    host = "",
                    count = 1,
                    timeout = 100
                }));

            AssertError(response);
        }

        [TestMethod]
        public async Task TestPing_InvalidAction_ReturnsError()
        {
            var response = await ExecuteAndGetResponse(
                CreateJob("ping", new
                {
                    action = "bogus",
                    host = "127.0.0.1",
                    count = 1,
                    timeout = 100
                }));

            AssertError(response);
        }
    }
}
