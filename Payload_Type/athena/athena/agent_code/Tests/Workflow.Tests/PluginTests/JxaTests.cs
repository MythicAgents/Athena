using System.Text.Json;
using Workflow.Contracts;
using Workflow.Tests.TestClasses;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class JxaTests : PluginTestBase
    {
        [TestMethod]
        public void Jxa_LoadsSuccessfully()
        {
            LoadPlugin("jxa");
            Assert.IsNotNull(_plugin);
            Assert.AreEqual("jxa", _plugin.Name);
        }

        [TestMethod]
        public async Task Jxa_EmptyArgs_ReturnsError()
        {
            LoadPlugin("jxa");
            var job = CreateJob("jxa", new { code = "", scriptcontents = "" });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
            AssertOutputContains(response, "No valid scripts");
        }

        [TestMethod]
        public async Task Jxa_NullCode_ReturnsError()
        {
            LoadPlugin("jxa");
            var job = CreateJob("jxa", new { });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
            AssertOutputContains(response, "No valid scripts");
        }

        [TestMethod]
        [TestCategory("macOS")]
        public async Task Jxa_WithCode_ExecutesOnMacOS()
        {
            if (!OperatingSystem.IsMacOS())
            {
                Assert.Inconclusive("JXA requires macOS");
                return;
            }

            LoadPlugin("jxa");
            var job = CreateJob("jxa", new { code = "1 + 1" });
            var response = await ExecuteAndGetResponse(job);
            Assert.IsNotNull(response);
            Assert.IsFalse(
                string.IsNullOrEmpty(response.user_output),
                "JXA should return output on macOS");
            Assert.IsFalse(
                response.user_output.StartsWith("Error"),
                $"JXA returned error: {response.user_output}");
            Assert.IsTrue(
                response.user_output.Trim() == "2",
                $"Expected '2' but got: {response.user_output}");
        }

        [TestMethod]
        [TestCategory("macOS")]
        public async Task Jxa_NonMacOS_ReturnsError()
        {
            if (OperatingSystem.IsMacOS())
            {
                Assert.Inconclusive(
                    "This test validates non-macOS behavior");
                return;
            }

            LoadPlugin("jxa");
            var job = CreateJob("jxa", new { code = "1+1" });
            var response = await ExecuteAndGetResponse(job);
            Assert.IsNotNull(response);
        }
    }
}
