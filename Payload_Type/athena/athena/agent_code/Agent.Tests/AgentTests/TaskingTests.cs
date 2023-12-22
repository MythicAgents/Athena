﻿using Agent.Models;
using Agent.Tests.TestClasses;
using Agent.Tests.TestInterfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.AgentTests
{
    [TestClass]
    public class TaskingTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        [TestMethod]
        public async Task TestGetTaskingSingle()
        {
            ManualResetEventSlim taskingReceived = new ManualResetEventSlim(false);
            IEnumerable<IProfile> _profile = new List<IProfile>() { new TestProfile()};
            _profile.First().SetTaskingReceived += (sender, args) => taskingReceived.Set();
            Agent _agent = new Agent(_profile, _taskManager, _logger, _config, _tokenManager, _cryptoManager);
            TestProfile prof = (TestProfile)_profile.First();

            Task.Run(() => _agent.Start());
            prof.taskingSent.WaitOne(1000);
            _profile.First().StopBeacon();
            Assert.IsTrue(((TestTaskManager)_taskManager).jobs.Count > 0);
        }
        [TestMethod]
        public async Task TestGetTaskingMultiple()
        {
            ManualResetEventSlim taskingReceived = new ManualResetEventSlim(false);
            Dictionary<string, string> args = new();

            args.Add("action", "connect");
            args.Add("username", "testuser");
            args.Add("password", "testpass");
            IEnumerable<IProfile> _profile = new List<IProfile>() { new TestProfile(
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

            _profile.First().SetTaskingReceived += (sender, args) => taskingReceived.Set();
            Agent _agent = new Agent(_profile, _taskManager, _logger, _config, _tokenManager, _cryptoManager);
            TestProfile prof = (TestProfile)_profile.First();

            Task.Run(_agent.Start);
            prof.taskingSent.WaitOne(1000);
            _profile.First().StopBeacon();
            Assert.IsTrue(((TestTaskManager)_taskManager).jobs.Count > 2);
        }
        [TestMethod]
        public async Task TestGetTaskingNoTasks() {
            ManualResetEvent taskingReceived = new ManualResetEvent(false);
            IEnumerable<IProfile> _profile = new List<IProfile>() { new TestProfile(
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
            Agent _agent = new Agent(_profile, _taskManager, _logger, _config, _tokenManager, _cryptoManager);
            Task.Run(_agent.Start);
            prof.taskingSent.WaitOne(1000);
            _profile.First().StopBeacon();
            Assert.IsTrue(((TestTaskManager)_taskManager).jobs.Count == 0);
        }
        [TestMethod]
        public async Task TestGetTaskingNullTasks()
        {
            ManualResetEvent taskingReceived = new ManualResetEvent(false);
            IEnumerable<IProfile> _profile = new List<IProfile>() { new TestProfile(true) };
            _profile.First().SetTaskingReceived += (sender, args) => taskingReceived.Set();
            TestProfile prof = (TestProfile)_profile.First();
            Agent _agent = new Agent(_profile, _taskManager, _logger, _config, _tokenManager, _cryptoManager);
            Task.Run(_agent.Start);
            prof.taskingSent.WaitOne(1000);
            _profile.First().StopBeacon();
            Assert.IsTrue(((TestTaskManager)_taskManager).jobs.Count == 0);
        }
    }
}