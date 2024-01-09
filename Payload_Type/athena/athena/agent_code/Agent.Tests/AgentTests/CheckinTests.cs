using Agent.Tests.TestClasses;
using Agent.Tests.TestInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Agent.Config;

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
        [TestMethod]
        public async Task TestSuccessfullCheckin()
        {
            string oldUuid = _config.uuid;
            Agent _agent = new Agent(_profiles, _taskManager, _logger, _config, _tokenManager);
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
            Agent _agent = new Agent(profile, _taskManager, _logger, _config, _tokenManager);
            var checkedIn = _agent.CheckIn().Result;
            Console.WriteLine(checkedIn);
            Assert.IsFalse(checkedIn);
        }
        [TestMethod]
        public void TestCheckinNull()
        {
            IEnumerable<IProfile> profile = new List<IProfile>() { new TestProfile(true) };
            Agent _agent = new Agent(profile, _taskManager, _logger, _config, _tokenManager);
            var checkedIn = _agent.CheckIn().Result;
            Console.WriteLine(checkedIn);
            Assert.IsFalse(checkedIn);
        }
    }
}
