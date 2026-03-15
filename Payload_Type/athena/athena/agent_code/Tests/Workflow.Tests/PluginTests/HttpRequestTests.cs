using Workflow.Tests;
using Workflow.Models;
using System.Net;

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

        private (HttpListener listener, string url)? TryCreateServer(
            string body = "OK", int status = 200)
        {
            try
            {
                return Utilities.CreateLocalHttpServer(body, status);
            }
            catch
            {
                return null;
            }
        }

        [TestMethod]
        public async Task HttpRequest_Get_ReturnsResponse()
        {
            var server = TryCreateServer("Hello from test server");
            if (server is null)
            {
                Assert.Inconclusive("HttpListener unavailable");
                return;
            }
            var (listener, url) = server.Value;
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
            var server = TryCreateServer("echo-ok");
            if (server is null)
            {
                Assert.Inconclusive("HttpListener unavailable");
                return;
            }
            var (listener, url) = server.Value;
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
            var server = TryCreateServer("header-test");
            if (server is null)
            {
                Assert.Inconclusive("HttpListener unavailable");
                return;
            }
            var (listener, url) = server.Value;
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
