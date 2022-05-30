using Microsoft.VisualStudio.TestTools.UnitTesting;
using Athena;
using PluginBase;
using Athena.Models.Mythic.Checkin;
using Athena.Config;

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
    }
}