using Workflow.Contracts;
using Workflow.Models;
using Workflow.Tests.TestClasses;
using Workflow.Tests.TestInterfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Tests.AgentTests
{
    [TestClass]
    public class TaskingTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        ISecurityProvider _cryptoManager = new TestCryptoManager();
        IRuntimeExecutor _spawner = new TestSpawner();
        IServiceExtension _agentMod = new TestAgentMod();
        [TestMethod]
        public async Task TestGetTaskingSingle()
        {
            ManualResetEventSlim taskingReceived = new ManualResetEventSlim(false);
            IEnumerable<IChannel> _profile = new List<IChannel>() { new TestProfile()};
            _profile.First().SetTaskingReceived += (sender, args) => taskingReceived.Set();
            ServiceHost _agent = new ServiceHost(_profile, _taskManager, _logger, _config, _tokenManager, new List<IServiceExtension>() { _agentMod });
            TestProfile prof = (TestProfile)_profile.First();

            Task.Run(() => _agent.Start());
            ((TestRequestDispatcher)_taskManager).WaitForNumberOfJobs(1);
            //prof.taskingSent.WaitOne(1000);
            _profile.First().StopBeacon();
            Console.WriteLine(((TestRequestDispatcher)_taskManager).jobs.Count);
            Assert.IsTrue(((TestRequestDispatcher)_taskManager).jobs.Count == 1);
        }
        [TestMethod]
        public async Task TestGetTaskingMultiple()
        {
            ManualResetEventSlim taskingReceived = new ManualResetEventSlim(false);
            Dictionary<string, string> args = new()
            {
                { "action", "connect" },
                { "username", "testuser" },
                { "password", "testpass" }
            };
            IEnumerable<IChannel> _profile = new List<IChannel>() { new TestProfile(
            new GetTaskingResponse()
            {
                action = "get_tasking",
                tasks = new List<ServerTask>()
                {
                    new ServerTask()
                    {
                        id = Guid.NewGuid().ToString(),
                        command = "whoami",
                        parameters = "",
                    },
                    new ServerTask()
                    {
                        id = Guid.NewGuid().ToString(),
                        command = "env",
                        parameters = "",
                    },
                    new ServerTask()
                    {
                        id = Guid.NewGuid().ToString(),
                        command = "ds",
                        parameters = System.Text.Json.JsonSerializer.Serialize(args)
                    }
                },
                socks = new List<ServerDatagram>(),
                rpfwd = new List<ServerDatagram>(),
                delegates = new List<DelegateMessage>(),
            }) };
            ServiceHost _agent = new ServiceHost(_profile, _taskManager, _logger, _config, _tokenManager, new List<IServiceExtension>() { _agentMod });
            TestProfile prof = (TestProfile)_profile.First();

            await _agent.Start();
            _profile.First().SetTaskingReceived += (sender, args) => taskingReceived.Set();

            ((TestRequestDispatcher)_taskManager).WaitForNumberOfJobs(3);
            //prof.taskingSent.WaitOne(1000);
            _profile.First().StopBeacon();
            Console.WriteLine(((TestRequestDispatcher)_taskManager).jobs.Count);
            Assert.IsTrue(((TestRequestDispatcher)_taskManager).jobs.Count == 3);
        }
        [TestMethod]
        public async Task TestGetTaskingNoTasks() {
            ManualResetEvent taskingReceived = new ManualResetEvent(false);
            IEnumerable<IChannel> _profile = new List<IChannel>() { new TestProfile(
            new GetTaskingResponse()
            {
                action = "get_tasking",
                tasks = new List<ServerTask>(),
                socks = new List<ServerDatagram>(),
                rpfwd = new List<ServerDatagram>(),
                delegates = new List<DelegateMessage>(),
            }) };
            _profile.First().SetTaskingReceived += (sender, args) => taskingReceived.Set();
            TestProfile prof = (TestProfile)_profile.First();
            ServiceHost _agent = new ServiceHost(_profile, _taskManager, _logger, _config, _tokenManager, new List<IServiceExtension>() { _agentMod });
            Task.Run(_agent.Start);
            prof.taskingSent.WaitOne(1000);
            _profile.First().StopBeacon();
            Assert.IsTrue(((TestRequestDispatcher)_taskManager).jobs.Count == 0);
        }
    }
}
