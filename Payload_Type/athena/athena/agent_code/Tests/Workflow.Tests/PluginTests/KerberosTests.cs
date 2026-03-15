using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class KerberosTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("kerberos");
        }

        [TestMethod]
        public async Task Kerberos_Klist_ReturnsOutput()
        {
            var job = CreateJob("kerberos", new { action = "klist" });
            var response = await ExecuteAndGetResponse(job);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                AssertSuccess(response);
            }
            else
            {
                AssertError(response);
            }
        }

        [TestMethod]
        public async Task Kerberos_UnknownAction_ReturnsError()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
                return;
            }
            var job = CreateJob("kerberos", new { action = "invalid" });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
