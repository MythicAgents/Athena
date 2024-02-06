using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class DownloadTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        IPlugin _downloadPlugin { get; set; }
        ServerJob _downloadJob { get; set; }
        public DownloadTests()
        {
            _downloadPlugin = PluginLoader.LoadPluginFromDisk("download", _messageManager, _config, _logger, _tokenManager, _spawner);
            _downloadJob = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "123",
                    command = "download",
                    token = 0,
                    parameters = "",

                }
            };
        }
        [TestMethod]
        public void TestPathParsingLocalFull()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "path", fileName },

            };

            Assert.IsTrue(false);
            //Test to make sure the plugin parses local paths like we expect
        }
        public void TestPathParsingUnc()
        {
            Assert.IsTrue(false);

            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestPathParsingRelative()
        {
            Assert.IsTrue(false);

            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestMultiChunkDownload()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Utilities.CreateTemporaryFileWithRandomText(fileName, 256000);
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "path", fileName },

            };
            Assert.IsTrue(false);

            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestSingleChunkDownload()
        {
            string fileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".txt";
            Utilities.CreateTemporaryFileWithRandomText(fileName, 512000 * 3);
            Dictionary<string, string> downloadParams = new Dictionary<string, string>()
            {
                { "path", fileName },

            };
            Assert.IsTrue(false);

            //Test to make sure the plugin parses local paths like we expect
        }
    }
}
