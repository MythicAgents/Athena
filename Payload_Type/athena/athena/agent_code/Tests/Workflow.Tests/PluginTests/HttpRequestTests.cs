using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class HttpRequestTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("http-request");
        }

        [TestMethod]
        public async Task HttpRequest_LoadsSuccessfully()
        {
            Assert.IsNotNull(_plugin);
            Assert.AreEqual("http-request", _plugin.Name);
        }

        [TestMethod]
        public async Task HttpRequest_MissingUrl_ReturnsError()
        {
            var job = CreateJob("http-request", new
            {
                url = "",
                method = "GET"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
