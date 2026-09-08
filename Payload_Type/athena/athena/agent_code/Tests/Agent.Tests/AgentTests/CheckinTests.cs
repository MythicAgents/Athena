using Agent.Tests.TestClasses;
using Agent.Tests.TestInterfaces;

namespace Agent.Tests.AgentTests
{
    [TestClass]
    public class CheckinTests
    {
        [TestMethod]
        public async Task AgentChecksInAndAdoptsServerIdentity()
        {
            var config = new TestAgentConfig();
            string oldUuid = config.uuid;
            var agent = new AthenaCore(
                new[] { new TestProfile() },
                new TestTaskManager(),
                new TestLogger(),
                config,
                new TestTokenManager(),
                new[] { new TestAgentMod() });

            Assert.IsTrue(await agent.CheckIn());
            Assert.AreNotEqual(oldUuid, config.uuid);
        }
    }
}
