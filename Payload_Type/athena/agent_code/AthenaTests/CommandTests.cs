using Microsoft.VisualStudio.TestTools.UnitTesting;
using Athena;
using Athena.Models.Mythic.Checkin;
using System.Threading.Tasks;
using System.Collections.Generic;
using Athena.Models.Mythic.Tasks;
using System;
using System.IO;
using System.Linq;
using Athena.Plugins;

namespace AthenaTests
{
    [TestClass]
    public class CommandTests
    {
        [TestMethod]
        public async Task TestPluginLoadAndExecute()
        {
            byte[] b = File.ReadAllBytes(@"../../../../AthenaPlugins/whoami/bin/Debug/net6.0/whoami.dll");
            string b64encode = Convert.ToBase64String(b);
            AthenaClient client = new AthenaClient();
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
                parameters = String.Empty,
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
            AthenaClient client = new AthenaClient();
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
            AthenaClient client = new AthenaClient();
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
            AthenaClient client = new AthenaClient();
            MythicTask task = new MythicTask()
            {
                command = "sleep",
                parameters = "{\"sleep\":\"100\",\"jitter\":\"5000\"}",
                id = "1"
            };

            await client.commandHandler.StartJob(task);

            Assert.IsTrue(client.currentConfig.sleep == 100 && client.currentConfig.jitter == 5000);
        }
        [TestMethod]
        public async Task TestExit()
        {
            AthenaClient client = new AthenaClient();
            MythicTask task = new MythicTask()
            {
                command = "exit",
                parameters = String.Empty,
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