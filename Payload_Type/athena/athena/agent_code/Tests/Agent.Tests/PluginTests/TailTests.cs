using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class TailTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        IPlugin _catPlugin { get; set; }
        public TailTests()
        {
            _catPlugin = PluginLoader.LoadPluginFromDisk("ls", _messageManager, _config, _logger, _tokenManager, _spawner);
        }
    }
}
