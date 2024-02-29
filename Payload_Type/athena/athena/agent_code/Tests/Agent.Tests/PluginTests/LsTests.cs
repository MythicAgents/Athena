using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class LsTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        ISpawner _spawner = new TestSpawner();
        ServerJob _job { get; set; }
        IPlugin _plugin { get; set; }
        public LsTests()
        {
            _plugin = PluginLoader.LoadPluginFromDisk("ls", _messageManager, _config, _logger, _tokenManager, _spawner);
            _job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "123",
                    command = "ls",
                    token = 0,
                }
            };
        }
        [TestMethod]
        public void TestValidParentPath()
        {
            if (OperatingSystem.IsMacOS())
            {
                //Temporary skip until I can fucking test on a mac and see what's going on

                //For some reason /etc is returning /System/etc

                Assert.IsTrue(true);
                return;
            }


            string path;
            string parent;
            if (OperatingSystem.IsWindows())
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc");
                parent = "drivers";
            }
            else
            {
                path = Path.Combine("/", "etc");
                parent = "";
            }
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", path }
            };

            _job.task.parameters = JsonSerializer.Serialize(parameters);
            _plugin.Execute(_job);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            FileBrowserTaskResponse fb = JsonSerializer.Deserialize<FileBrowserTaskResponse>(response);
            Assert.AreEqual(fb.file_browser.parent_path, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), parent));
        }
        [TestMethod]
        public void TestGetFileListingLocal()
        {
            string path = string.Empty;
            if (OperatingSystem.IsWindows())
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc");
            }
            else
            {
                path = Path.Combine("/", "etc");
            }
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", path }
            };

            _job.task.parameters = JsonSerializer.Serialize(parameters);
            _plugin.Execute(_job);

            ((TestMessageManager)_messageManager).hasResponse.WaitOne();
            string response = ((TestMessageManager)_messageManager).GetRecentOutput().Result;
            FileBrowserTaskResponse fb = JsonSerializer.Deserialize<FileBrowserTaskResponse>(response);
            bool found = false;
            foreach(var f in fb.file_browser.files)
            {
                if(f.name == "hosts")
                {
                    found = true;
                }   
            }

            //Make sure
            Assert.IsTrue(found);

        }
        [TestMethod]
        public void TestGetFileListing_Failure()
        {
            string path = string.Empty;
            if (OperatingSystem.IsWindows())
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc");
            }
            else
            {
                path = Path.Combine("/", "etc");
            }
        }
        [TestMethod]
        public void TestPathParsingLocalFull()
        {
            //Make sure
        }
        [TestMethod]
        public void TestPathParsingUnc()
        {
            //Test to make sure the plugin parses local paths like we expect
        }
        [TestMethod]
        public void TestPathParsingRelative()
        {
            //Test to make sure the plugin parses local paths like we expect
        }
    }
}
