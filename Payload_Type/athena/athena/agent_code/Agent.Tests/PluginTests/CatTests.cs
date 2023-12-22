using Agent.Tests.TestClasses;
using Agent.Tests.TestInterfaces;
using cat;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class CatTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        [TestMethod]
        public async Task TestCatPlugin_FileExists()
        {
            string tempFile = Path.GetTempFileName();

            string stringToCompare = "I could not bring myself to fight my Father’s brother, Poseidon, quaking with anger at you, still enraged";

            File.WriteAllText(tempFile, stringToCompare);

            cat.Config _cat = new cat.Config(_messageManager, _config, _logger, _tokenManager);
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", tempFile }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "cat"
                }
            };

            await _cat.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(output.Equals(stringToCompare));

            File.Delete(tempFile);
        }
        [TestMethod]
        public async Task TestCatPlugin_FileNotFound()
        {
            string tempFile = Path.Combine(Path.GetTempPath(),"Idontexistasdfewrwerw.txt");

            //string stringToCompare = "I could not bring myself to fight my Father’s brother, Poseidon, quaking with anger at you, still enraged";

            //File.WriteAllText(tempFile, stringToCompare);

            cat.Config _cat = new cat.Config(_messageManager, _config, _logger, _tokenManager);
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", tempFile }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "cat"
                }
            };

            await _cat.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(output.Contains("Could not find file"));
        }
        [TestMethod]
        public async Task TestCatPlugin_EmptyFile()
        {
            string tempFile = Path.GetTempFileName();

            //string stringToCompare = "I could not bring myself to fight my Father’s brother, Poseidon, quaking with anger at you, still enraged";

            //File.WriteAllText(tempFile, stringToCompare);

            cat.Config _cat = new cat.Config(_messageManager, _config, _logger, _tokenManager);
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "path", tempFile }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "cat"
                }
            };

            await _cat.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            //Assert.IsTrue(output.Equals(stringToCompare));
            Assert.IsTrue(String.IsNullOrEmpty(output));
            File.Delete(tempFile);
        }
    }
}
