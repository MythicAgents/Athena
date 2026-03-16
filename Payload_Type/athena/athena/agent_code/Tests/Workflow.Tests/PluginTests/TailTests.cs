using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class TailTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        ISecurityProvider _cryptoManager = new TestCryptoManager();
        IDataBroker _messageManager = new TestDataBroker();
        IRuntimeExecutor _spawner = new TestSpawner();
        IModule _catPlugin { get; set; }
        public TailTests()
        {
            _catPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("tail");
        }
    }
}
