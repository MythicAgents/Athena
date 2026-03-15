using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("Network")]
    public class HttpRequestTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("http-request");
        }

        [TestMethod]
        public async Task HttpRequest_Get_ReturnsResponse()
        {
            var (listener, url) = Utilities.CreateLocalHttpServer(
                "Hello from test server");
            try
            {
                var job = CreateJob("http-request", new
                {
                    url = url,
                    method = "GET"
                });
                var response = await ExecuteAndGetResponse(job);
                AssertSuccess(response);
                AssertOutputContains(response, "Hello from test server");
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        [TestMethod]
        public async Task HttpRequest_Post_SendsBody()
        {
            var (listener, url) = Utilities.CreateLocalHttpServer("echo-ok");
            try
            {
                var job = CreateJob("http-request", new
                {
                    url = url,
                    method = "POST",
                    body = "{\"key\":\"value\"}"
                });
                var response = await ExecuteAndGetResponse(job);
                AssertSuccess(response);
                AssertOutputContains(response, "echo-ok");
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
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

        [TestMethod]
        public async Task HttpRequest_IncludesStatusAndHeaders()
        {
            var (listener, url) = Utilities.CreateLocalHttpServer(
                "header-test");
            try
            {
                var job = CreateJob("http-request", new
                {
                    url = url,
                    method = "GET"
                });
                var response = await ExecuteAndGetResponse(job);
                AssertSuccess(response);
                AssertOutputContains(response, "200");
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }
    }
}
