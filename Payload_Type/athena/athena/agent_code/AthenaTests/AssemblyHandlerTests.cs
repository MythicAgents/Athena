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
using Athena.Commands;
using Athena.Models;
using System.Text.Json;

namespace AthenaTests
{
    [TestClass]
    public class AssemblyHandlerTests
    {
        [TestMethod]
        public async Task TestPluginLoadAndExecute()
        {
            byte[] b = File.ReadAllBytes(@"../../../../AthenaPlugins/bin/whoami.dll");

            AssemblyHandler ah = new AssemblyHandler();

            string res = await ah.LoadCommandAsync("1","whoami",b);

            Assert.IsTrue(res.Contains("Command loaded"));

            MythicTask task2 = new MythicTask()
            {
                command = "whoami",
                parameters = String.Empty,
                id = "2"
            };
            
            var mj = new MythicJob(task2);

            await ah.RunLoadedCommand(mj);

            List<string> listres = await PluginHandler.GetResponses();
            Assert.IsTrue(listres.First().Contains(Environment.UserName));
        }
        [TestMethod]
        public async Task TestPluginLoadInvalid()
        {
            MythicTask task = new MythicTask()
            {
                command = "load",
                parameters = "{\"command\":\"whoami\",\"assembly\":\"aGVsbG93b3JsZA==\"}",
                id = "1"
            };

            MythicJob job = new MythicJob(task);

            AssemblyHandler ah = new AssemblyHandler();

            var res = await ah.LoadAssemblyAsync(job);

            Assert.IsTrue(res.Contains("error"));
        }
        [TestMethod]
        public async Task TestPluginLoadEmpty()
        {
            MythicTask task = new MythicTask()
            {
                command = "load",
                parameters = "{\"command\":\"whoami\",\"assembly\":\"\"}",
                id = "1"
            };

            MythicJob job = new MythicJob(task);

            AssemblyHandler ah = new AssemblyHandler();

            var res = await ah.LoadAssemblyAsync(job);

            Assert.IsTrue(res.Contains("error"));
        }
        [TestMethod]
        public async Task TestSleepAndJitter()
        {
            //AthenaClient client = new AthenaClient();
            //MythicTask task = new MythicTask()
            //{
            //    command = "sleep",
            //    parameters = "{\"sleep\":\"100\",\"jitter\":\"5000\"}",
            //    id = "1"
            //};

            //await client.commandHandler.StartJob(task);

            //Assert.IsTrue(client.currentConfig.sleep == 100 && client.currentConfig.jitter == 5000);
        }
        [TestMethod]
        public async Task TestExit()
        {
            //AthenaClient client = new AthenaClient();
            //MythicTask task = new MythicTask()
            //{
            //    command = "exit",
            //    parameters = String.Empty,
            //    id = "1"
            //};

            //await client.commandHandler.StartJob(task);

            //Assert.IsTrue(client.exit);
        }
    }
}