using System.Text.Json;
using Workflow.Tests.TestClasses;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class SmbPluginTests
    {
        private TestDataBroker _messageMgr;
        private TestServiceConfig _config;
        private TestLogger _logger;
        private IModule _plugin;

        [TestInitialize]
        public void Setup()
        {
            _messageMgr = new TestDataBroker();
            _config = new TestServiceConfig();
            _logger = new TestLogger();
            _plugin = new PluginLoader(_messageMgr, _config)
                .LoadPluginFromDisk("smb");
        }

        [TestMethod]
        public async Task Execute_InvalidParameters_ReturnsError()
        {
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "task-1",
                    command = "smb",
                    parameters = "not-valid-json{{{",
                }
            };

            await _plugin.Execute(job);

            Assert.AreEqual(1, _messageMgr.taskResponses.Count);
            var output = _messageMgr.GetRecentOutput();
            Assert.IsTrue(output.Contains("error"));
        }

        [TestMethod]
        public async Task Execute_UnlinkMissingLink_ReturnsFailure()
        {
            var args = new SmbLinkArgs
            {
                action = "unlink",
                pipename = "nonexistent",
                hostname = ".",
            };
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "task-unlink",
                    command = "smb",
                    parameters = JsonSerializer.Serialize(args),
                }
            };

            await _plugin.Execute(job);

            Assert.AreEqual(1, _messageMgr.taskResponses.Count);
            var output = _messageMgr.GetRecentOutput();
            Assert.IsTrue(output.Contains("Failed to unlink"));
        }

        [TestMethod]
        public async Task Execute_List_ReturnsResponse()
        {
            var args = new SmbLinkArgs
            {
                action = "list",
                pipename = "test",
                hostname = ".",
            };
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "task-list",
                    command = "smb",
                    parameters = JsonSerializer.Serialize(args),
                }
            };

            await _plugin.Execute(job);

            Assert.AreEqual(1, _messageMgr.taskResponses.Count);
        }

        [TestMethod]
        public async Task Execute_UnknownAction_ReturnsError()
        {
            var args = new SmbLinkArgs
            {
                action = "foobar",
                pipename = "test",
                hostname = ".",
            };
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "task-unknown",
                    command = "smb",
                    parameters = JsonSerializer.Serialize(args),
                }
            };

            await _plugin.Execute(job);

            Assert.AreEqual(1, _messageMgr.taskResponses.Count);
            var output = _messageMgr.GetRecentOutput();
            Assert.IsTrue(output.Contains("Unknown action"));
        }
    }
}
