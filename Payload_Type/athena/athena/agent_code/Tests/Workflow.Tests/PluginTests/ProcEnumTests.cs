using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class ProcEnumTests : PluginTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            LoadPlugin("proc-enum");
        }

        [TestMethod]
        public async Task ProcEnum_ListsProcesses()
        {
            var job = CreateJob("proc-enum", new { action = "proc-enum" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            AssertOutputContains(response, "pid");
            AssertOutputContains(response, "name");
        }

        [TestMethod]
        public async Task ProcEnum_ContainsCurrentProcess()
        {
            var job = CreateJob("proc-enum", new { action = "proc-enum" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            AssertOutputContains(response, "dotnet");
        }

        [TestMethod]
        public async Task ProcEnum_NamedPipes_Windows()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Inconclusive("Windows only");
                return;
            }
            var job = CreateJob("proc-enum", new { action = "named-pipes" });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
        }

        [TestMethod]
        public async Task ProcEnum_UnknownAction_ReturnsError()
        {
            var job = CreateJob("proc-enum", new { action = "invalid" });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
