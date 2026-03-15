using Workflow.Tests;
using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    [TestCategory("Execution")]
    public class ExecutionTests : PluginTestBase
    {
        [TestMethod]
        public async Task Exec_MissingCommandline_ReturnsMessage()
        {
            LoadPlugin("exec");
            var job = CreateJob("exec", new
            {
                commandline = "",
                output = false
            });
            var response = await ExecuteAndGetResponse(job);
            Assert.IsNotNull(response);
            AssertOutputContains(response, "Missing commandline");
        }

        [TestMethod]
        public async Task Exec_NullArgs_ReturnsMessage()
        {
            LoadPlugin("exec");
            var job = CreateJob("exec", new { });
            var response = await ExecuteAndGetResponse(job);
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task ExecuteAssembly_MissingAsm_ReturnsError()
        {
            LoadPlugin("execute-assembly");
            var job = CreateJob("execute-assembly", new
            {
                asm = "",
                arguments = ""
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
            AssertOutputContains(response, "Missing Assembly Bytes");
        }

        [TestMethod]
        public async Task InjectShellcode_MissingArgs_ReturnsError()
        {
            LoadPlugin("inject-shellcode");
            var job = CreateJob("inject-shellcode", new
            {
                pid = 0,
                commandline = "",
                asm = ""
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }

        [TestMethod]
        public async Task InjectShellcode_NoPidNoCommandline_ReturnsError()
        {
            LoadPlugin("inject-shellcode");
            var job = CreateJob("inject-shellcode", new
            {
                pid = 0,
                commandline = "",
                asm = "AAAA"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
            AssertOutputContains(response, "pid or command line");
        }

        [TestMethod]
        public async Task Coff_InvalidBof_ReturnsError()
        {
            LoadPlugin("coff");
            var job = CreateJob("coff", new
            {
                asm = "not-valid-base64-bof",
                functionName = "go",
                arguments = ""
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }

        [TestMethod]
        public async Task Shell_LoadsSuccessfully()
        {
            LoadPlugin("shell");
            Assert.IsNotNull(_plugin);
            Assert.AreEqual("shell", _plugin.Name);
        }

        [TestMethod]
        public async Task InjectShellcodeMacos_NonMacOS_ReturnsError()
        {
            if (System.Runtime.InteropServices.RuntimeInformation
                .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                Assert.Inconclusive("This test is for non-macOS platforms");
            }
            LoadPlugin("inject-shellcode-macos");
            var job = CreateJob("inject-shellcode-macos", new
            {
                pid = 1234,
                asm = "AAAA"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
            AssertOutputContains(response, "only available on macOS");
        }

        [TestMethod]
        public async Task InjectShellcodeMacos_MissingArgs_ReturnsError()
        {
            LoadPlugin("inject-shellcode-macos");
            var job = CreateJob("inject-shellcode-macos", new
            {
                pid = 0,
                commandline = "",
                asm = ""
            });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
