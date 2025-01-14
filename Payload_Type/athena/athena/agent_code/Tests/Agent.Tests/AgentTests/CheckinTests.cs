using Agent.Tests.TestClasses;
using Agent.Tests.TestInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.AgentTests
{
    [TestClass]
    public class CheckinTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        IAgentMod _agentMod = new TestAgentMod();
        [TestMethod]
        public async Task TestSuccessfullCheckin()
        {
            string oldUuid = _config.uuid;
            AthenaCore _agent = new AthenaCore(_profiles, _taskManager, _logger, _config, _tokenManager, new List<IAgentMod>() { _agentMod });
            var checkedIn = await _agent.CheckIn();
            Assert.IsTrue(checkedIn && (_config.uuid != oldUuid));
        }
        [TestMethod]
        public void TestCheckinEmpty()
        {
            IEnumerable<IProfile> profile = new List<IProfile>() { new TestProfile(new CheckinResponse()
            {
                status = "failed",
                action = "checkin",
                id = Guid.NewGuid().ToString(),
                encryption_key = "",
                decryption_key = "",
                process_name = "",
            }) };
            AthenaCore _agent = new AthenaCore(profile, _taskManager, _logger, _config, _tokenManager, new List<IAgentMod>() { _agentMod });
            var checkedIn = _agent.CheckIn().Result;
            Console.WriteLine(checkedIn);
            Assert.IsFalse(checkedIn);
        }
    }
}
