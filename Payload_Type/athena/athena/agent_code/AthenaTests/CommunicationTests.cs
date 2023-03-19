using Microsoft.VisualStudio.TestTools.UnitTesting;
using Athena;
using Athena.Models.Mythic.Checkin;
using System.Threading.Tasks;
using System.Collections.Generic;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Config;
using System;
using Athena.Models.Mythic.Response;
using System.Linq;
using Athena.Utilities;
using System.Text;
using Athena.Models;
using Athena.Commands;

namespace AthenaTests
{
    [TestClass]
    public class CommunicationTests
    {
        [TestMethod]
        public void TestCheckin()
        {
            //AthenaClient client = new AthenaClient();
            //CheckinResponse res = client.handleCheckin().Result;
            //Assert.IsTrue(res.status == "success");
        }

        [TestMethod]
        public async Task TestCheckinAndGetTask()
        {
            //AthenaClient client = new AthenaClient();
            //CheckinResponse res = client.handleCheckin().Result;
            //if (await client.updateAgentInfo(res))
            //{
            //    var delegateMessages = await client.forwarder.GetMessages();
            //    var socksMessages = await client.socksHandler.GetMessages();
            //    var responses = await client.commandHandler.GetResponses();
            //    List<MythicTask> tasks = await client.GetTasks();
            //    Assert.IsNotNull(tasks);
            //}

            //Assert.Fail();
        }

        [TestMethod]
        public async Task Test1MBSMBTransfer()
        {

        }

        [TestMethod]
        public async Task Test10MBSMBTransfer()
        {
            //Athena.Smb smb = new Athena.Smb();
            //smb.pipeName = "myPipe";
            //smb.psk = "";

            //string myString = new string('A', (131072 * 10));


            //System.Threading.Thread.Sleep(2000);

            //Athena.Forwarders.Forwarder fwd = new Athena.Forwarders.Forwarder();

            //Dictionary<string, string> blah = new Dictionary<string, string>
            //{
            //    {"pipename","pipename" },
            //    {"hostname", "localhost" }
            //};

            //string json = System.Text.Json.JsonSerializer.Serialize(blah);
            //DelegateMessage dm = new DelegateMessage()
            //{
            //    c2_profile = "smb",
            //    message = myString,
            //    final = true,
            //    uuid = "1"
            //};

            ////smb.Send(System.Text.Json.JsonSerializer.Serialize(dm));

            //await fwd.Link(new MythicJob
            //{
            //    task = new MythicTask
            //    {
            //        id = "1",
            //        parameters = json,
            //    }
            //}, "BlahBlahBlah");


            ////Wait for SMB to realize it's connected.
            //while (!await smb.IsConnected()) { }

            //Assert.IsTrue(await smb.IsConnected());
            //Assert.IsTrue(fwd.connected);

            //smb.Send(System.Text.Json.JsonSerializer.Serialize(dm));
            ////Assert.IsTrue(await smb.IsConnected());

            //var messages = await fwd.GetMessages();
            //while (messages.Count < 1)
            //{
            //    messages = await fwd.GetMessages();
            //}

            //Assert.IsTrue(messages.Count > 0);
            //string b64Message = await Misc.Base64Decode(messages.First().message);

            //DelegateMessage dm2 = System.Text.Json.JsonSerializer.Deserialize<DelegateMessage>(b64Message.Substring(6));
            //Assert.IsTrue(dm2.message.Equals(dm.message));
        }

        [TestMethod]
        public async Task Test1GBSMBTransfer()
        {
            //Athena.Smb smb = new Athena.Smb();
            //smb.pipeName = "myPipe";
            //smb.psk = "";

            //string myString = new string('A', (131072*1024));


            //System.Threading.Thread.Sleep(2000);

            //Athena.Forwarders.Forwarder fwd = new Athena.Forwarders.Forwarder();

            //Dictionary<string, string> blah = new Dictionary<string, string>
            //{
            //    {"pipename","pipename" },
            //    {"hostname", "localhost" }
            //};

            //string json = System.Text.Json.JsonSerializer.Serialize(blah);
            //DelegateMessage dm = new DelegateMessage()
            //{
            //    c2_profile = "smb",
            //    message = myString,
            //    final = true,
            //    uuid = "1"
            //};

            ////smb.Send(System.Text.Json.JsonSerializer.Serialize(dm));

            //await fwd.Link(new MythicJob
            //{
            //    task = new MythicTask
            //    {
            //        id = "1",
            //        parameters = json,
            //    }
            //}, "BlahBlahBlah");


            ////Wait for SMB to realize it's connected.
            //while (!await smb.IsConnected()) { }

            //Assert.IsTrue(await smb.IsConnected());
            //Assert.IsTrue(fwd.connected);

            //smb.Send(System.Text.Json.JsonSerializer.Serialize(dm));
            ////Assert.IsTrue(await smb.IsConnected());

            //var messages = await fwd.GetMessages();
            //while (messages.Count < 1)
            //{
            //    messages = await fwd.GetMessages();
            //}

            //Assert.IsTrue(messages.Count > 0);
            //string b64Message = await Misc.Base64Decode(messages.First().message);

            //DelegateMessage dm2 = System.Text.Json.JsonSerializer.Deserialize<DelegateMessage>(b64Message.Substring(6));
            //Assert.IsTrue(dm2.message.Equals(dm.message));
        }
    }
}
