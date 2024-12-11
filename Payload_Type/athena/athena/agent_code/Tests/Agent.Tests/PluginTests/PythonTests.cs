using Agent.Models;
using Agent.Utilities;
using System.Reflection;
using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class PythonTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        IPythonManager _pythonManager;
        IPlugin _pythonExecPlugin { get; set; }
        IPlugin _pythonLoadPlugin { get; set; }
        public PythonTests()
        {
            _pythonManager = GetPythonManager();
            _pythonExecPlugin = PluginLoader.LoadPluginFromDisk("python-exec", _messageManager, _config, _logger, _tokenManager, null, _pythonManager);
            _pythonLoadPlugin = PluginLoader.LoadPluginFromDisk("python-load", _messageManager, _config, _logger, _tokenManager, null, _pythonManager);
        }

        private IPythonManager GetPythonManager()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "Agent.Managers.Python", "bin", "Debug", "net8.0", $"Agent.Managers.Python.dll");
            byte[] buf = File.ReadAllBytes(path);
            Assembly asm = Assembly.Load(buf);

            return this.ParseAssemblyForPlugin(asm);
        }
        private IPythonManager ParseAssemblyForPlugin(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IPythonManager).IsAssignableFrom(t))
                {
                    IPythonManager manager = (IPythonManager)Activator.CreateInstance(t);
                    return manager;
                }
            }
            return null;
        }

        [TestMethod]
        public async Task TestLibraryLoad()
        {
            string embeddedZip = Misc.Base64Encode(ExtractResource("python313.zip"));

            Assert.IsTrue(embeddedZip.Length > 0);

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "file", embeddedZip},
            };

            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "python-load"
                }
            };

            await _pythonLoadPlugin.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(output.Contains("Loaded."));
            Console.WriteLine(output);
        }
        [TestMethod]
        public async Task TestStandaloneScript()
        {

        }
        [TestMethod]
        public async Task TestLibraryScript()
        {
            string embeddedZip = Misc.Base64Encode(ExtractResource("python313.zip"));

            Assert.IsTrue(embeddedZip.Length > 0);

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "file", embeddedZip},
            };

            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "1",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "python-load"
                }
            };

            await _pythonLoadPlugin.Execute(job);
            var mm = (TestMessageManager)_messageManager;
            string output = await mm.GetRecentOutput();
            Assert.IsTrue(output.Contains("Loaded."));


            string script = @"
import sys

def main():
    print('Arguments passed to __main__: ', sys.argv)

main()
";


            parameters = new Dictionary<string, object>
            {
                { "file", Misc.Base64Encode(script)},
                { "args", "myarg1 myarg3 \"my arg 4\"" }
            };
            job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "2",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "python-exec"
                }
            };
            await _pythonExecPlugin.Execute(job);
            Thread.Sleep(1000);
            output = await mm.GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(output);

            Assert.IsTrue(rr.user_output.Contains("myarg1") && rr.user_output.Contains("myarg3") && rr.user_output.Contains("my arg 4"));

            Console.WriteLine(rr.user_output);
        }
        public static byte[] ExtractResource(string filename)
        {
            List<string> sources = Assembly.GetExecutingAssembly().GetManifestResourceNames().ToList();
            Stream resFilestream = Assembly.GetExecutingAssembly().GetManifestResourceStream(sources.Find(item => item.Contains(filename)));
            if (resFilestream == null) return null;
            byte[] ba = new byte[resFilestream.Length];
            resFilestream.Read(ba, 0, ba.Length);
            return ba;
        }
    }
}
