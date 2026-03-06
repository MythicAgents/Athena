using Workflow.Tests.TestClasses;
using Workflow.Tests.TestInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Tests.AgentTests
{
    [TestClass]
    public class CheckinTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        IServiceExtension _agentMod = new TestAgentMod();
        [TestMethod]
        public async Task TestSuccessfullCheckin()
        {
            string oldUuid = _config.uuid;
            ServiceHost _agent = new ServiceHost(_profiles, _taskManager, _logger, _config, _tokenManager, new List<IServiceExtension>() { _agentMod });
            var checkedIn = await _agent.CheckIn();
            Assert.IsTrue(checkedIn && (_config.uuid != oldUuid));
        }
        [TestMethod]
        public void TestCheckinEmpty()
        {
            IEnumerable<IChannel> profile = new List<IChannel>() { new TestProfile(new CheckinResponse()
            {
                status = "failed",
                action = "checkin",
                id = Guid.NewGuid().ToString(),
                encryption_key = "",
                decryption_key = "",
                process_name = "",
            }) };
            ServiceHost _agent = new ServiceHost(profile, _taskManager, _logger, _config, _tokenManager, new List<IServiceExtension>() { _agentMod });
            var checkedIn = _agent.CheckIn().Result;
            Console.WriteLine(checkedIn);
            Assert.IsFalse(checkedIn);
        }
    }
}
