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
            _pythonExecPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("python-exec");
            _pythonLoadPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("python-load");
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
                { "file", "123"},
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
            UploadTaskResponse ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestMessageManager)_messageManager).GetRecentOutput());
            Assert.IsTrue(ur is not null);
            //Test to make sure the plugin parses local paths like we expect

            //Call HandleNextChunk
            ServerTaskingResponse responseResult = new ServerTaskingResponse()
            {
                task_id = "1",
                file_id = "123",
                total_chunks = 1,
                chunk_data = embeddedZip,
                chunk_num = 1,
            };
            await ((IFilePlugin)_pythonLoadPlugin).HandleNextMessage(responseResult);
            string response = ((TestMessageManager)_messageManager).GetRecentOutput();
            ur = JsonSerializer.Deserialize<UploadTaskResponse>(response);
            Assert.IsTrue(ur.user_output.Contains("Loaded."));
        }
        [TestMethod]
        public async Task TestStandaloneScript()
        {

        }
        [TestMethod]
        public async Task TestStdLibScript()
        {
            string embeddedZip = Misc.Base64Encode(ExtractResource("python313.zip"));

            Assert.IsTrue(embeddedZip.Length > 0);

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "file", embeddedZip},
            };

            //ServerJob job = new ServerJob()
            //{
            //    task = new ServerTask()
            //    {
            //        id = "1",
            //        parameters = JsonSerializer.Serialize(parameters),
            //        command = "python-load"
            //    }
            //};

            //await _pythonLoadPlugin.Execute(job);
            //var mm = (TestMessageManager)_messageManager;
            //string output = ((TestMessageManager)_messageManager).GetRecentOutput();
            //Console.WriteLine(output);

            //Assert.IsTrue(output.Contains("Loaded."));


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
            ServerJob job = new ServerJob()
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
            string output = ((TestMessageManager)_messageManager).GetRecentOutput();
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
