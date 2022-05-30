using Microsoft.VisualStudio.TestTools.UnitTesting;
using Athena;
using PluginBase;
using Athena.Models.Mythic.Checkin;
using Athena.Config;
using System.Threading.Tasks;
using System.Collections.Generic;
using Athena.Models.Mythic.Tasks;
using System;
using System.IO;
using System.Linq;

namespace AthenaTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestCheckin()
        {
            MythicClient client = new MythicClient();
            CheckinResponse res = client.handleCheckin().Result;

            Assert.IsTrue(res.status == "success");
        }
        [TestMethod]
        public async Task TestCheckinAndGetTask()
        {
            MythicClient client = new MythicClient();
            CheckinResponse res = client.handleCheckin().Result;
            if (await client.updateAgentInfo(res))
            {
                var delegateMessages = await client.MythicConfig.forwarder.GetMessages();
                var socksMessages = await client.socksHandler.GetMessages();
                var responses = await client.commandHandler.GetResponses();
                List<MythicTask> tasks = await client.GetTasks(responses, delegateMessages, socksMessages);
                Assert.IsNotNull(tasks);
            }
            else
            {
                Assert.Fail();
            }
            
        }
        [TestMethod]
        public async Task TestPluginLoadAndExecute()
        {
            byte[] b = File.ReadAllBytes(@"../../../../AthenaPlugins/whoami/bin/Debug/net6.0/whoami.dll");
            string b64encode = Convert.ToBase64String(b);
            MythicClient client = new MythicClient();
            MythicTask task = new MythicTask()
            {
                command = "load",
                parameters = "{\"command\":\"whoami\",\"assembly\":\"" + b64encode + "\"}",
                id = "1"
            };
            Task t = client.commandHandler.StartJob(task);
            Task.WaitAll(t);

            MythicTask task2 = new MythicTask()
            {
                command = "whoami",
                parameters = "",
                id = "2"
            };

            Task t2 = client.commandHandler.StartJob(task2);
            Task.WaitAll(t2);
            List<object> responses = await client.commandHandler.GetResponses();

            ResponseResult rr = (ResponseResult)responses.FirstOrDefault();
            Console.WriteLine(rr.user_output);
            Assert.IsTrue(rr.user_output.Contains(Environment.UserName));
        }
        [TestMethod]
        public async Task TestPluginLoadInvalid()
        {
            MythicClient client = new MythicClient();
            MythicTask task = new MythicTask()
            {
                command = "load",
                parameters = "{\"command\":\"whoami\",\"assembly\":\"aGVsbG93b3JsZA==\"}",
                id = "1"
            };
            Task t = client.commandHandler.StartJob(task);
            Task.WaitAll(t);
            List<object> responses = await client.commandHandler.GetResponses();

            ResponseResult rr = (ResponseResult)responses.FirstOrDefault();
            Console.WriteLine(rr.user_output);
            Assert.IsTrue(rr.status == "error");
        }
        [TestMethod]
        public async Task TestPluginLoadEmpty()
        {
            MythicClient client = new MythicClient();
            MythicTask task = new MythicTask()
            {
                command = "load",
                parameters = "{\"command\":\"whoami\",\"assembly\":\"\"}",
                id = "1"
            };
            Task t = client.commandHandler.StartJob(task);
            Task.WaitAll(t);
            List<object> responses = await client.commandHandler.GetResponses();

            ResponseResult rr = (ResponseResult)responses.FirstOrDefault();
            Console.WriteLine(rr.user_output);
            Assert.IsTrue(rr.status == "error");
        }
        [TestMethod]
        public async Task TestSleepAndJitter()
        {
            MythicClient client = new MythicClient();
            MythicTask task = new MythicTask()
            {
                command = "sleep",
                parameters = "{\"sleep\":\"100\",\"jitter\":\"5000\"}",
                id = "1"
            };

            await client.commandHandler.StartJob(task);

            Assert.IsTrue(client.MythicConfig.sleep == 100 && client.MythicConfig.jitter == 5000);
        }
        [TestMethod]
        public async Task TestExit()
        {
            MythicClient client = new MythicClient();
            MythicTask task = new MythicTask()
            {
                command = "exit",
                parameters = "",
                id = "1"
            };

            await client.commandHandler.StartJob(task);

            Assert.IsTrue(client.exit);
        }
        [TestMethod]
        public async Task TestParseSocksMessage()
        {
            Assert.IsTrue(true);
        }

    }
}