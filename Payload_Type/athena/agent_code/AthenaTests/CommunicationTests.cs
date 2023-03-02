using Microsoft.VisualStudio.TestTools.UnitTesting;
using Athena;
using Athena.Models.Mythic.Checkin;
using System.Threading.Tasks;
using System.Collections.Generic;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Config;

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
        public async Task TestSMBTransfer()
        {
            Athena.Smb smb = new Athena.Smb();
            smb.pipeName = "myPipe";
            smb.psk = "";

            Athena.Forwarders.Forwarder fwd = new Athena.Forwarders.Forwarder();
            Dictionary<string, string> blah = new Dictionary<string, string>
            {
                {"pipename","myPipe" },
                {"hostname", "localhost" }
            };

            string json = System.Text.Json.JsonSerializer.Serialize(blah);

            await fwd.Link(new MythicJob
            {
                task = new MythicTask
                {
                    id = "1",
                    parameters = json,
                }
            }, "BlahBlahBlah");

        }
    }
}
