using Microsoft.VisualStudio.TestTools.UnitTesting;
using Athena;
using Athena.Models.Mythic.Checkin;
using System.Threading.Tasks;
using System.Collections.Generic;
using Athena.Models.Mythic.Tasks;

namespace AthenaTests
{
    [TestClass]
    public class CommunicationTests
    {
        [TestMethod]
        public void TestCheckin()
        {
            AthenaClient client = new AthenaClient();
            CheckinResponse res = client.handleCheckin().Result;
            Assert.IsTrue(res.status == "success");
        }

        [TestMethod]
        public async Task TestCheckinAndGetTask()
        {
            AthenaClient client = new AthenaClient();
            CheckinResponse res = client.handleCheckin().Result;
            if (await client.updateAgentInfo(res))
            {
                var delegateMessages = await client.forwarder.GetMessages();
                var socksMessages = await client.socksHandler.GetMessages();
                var responses = await client.commandHandler.GetResponses();
                List<MythicTask> tasks = await client.GetTasks();
                Assert.IsNotNull(tasks);
            }
            else
            {
                Assert.Fail();
            }

        }
    }
}
