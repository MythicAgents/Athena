using Workflow.Models;
using Workflow.Utilities;
using System.Reflection;
using System.Text.Json;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class PythonTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        ISecurityProvider _cryptoManager = new TestCryptoManager();
        IDataBroker _messageManager = new TestDataBroker();
        IScriptEngine _pythonManager;
        IModule _pythonExecPlugin { get; set; }
        IModule _pythonLoadPlugin { get; set; }
        public PythonTests()
        {
            _pythonManager = GetScriptEngine();
            _pythonExecPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("python-exec");
            _pythonLoadPlugin = new PluginLoader(_messageManager).LoadPluginFromDisk("python-load");
        }

        private IScriptEngine GetScriptEngine()
        {
            var cwd = Directory.GetCurrentDirectory();
            var configDir = new DirectoryInfo(cwd).Parent?.Name ?? "Debug";
            var path = Path.Combine(cwd, "..", "..", "..", "..", "..", "Workflow.Providers.Script", "bin", configDir, "net10.0", $"Workflow.Providers.Script.dll");
            byte[] buf = File.ReadAllBytes(path);
            Assembly asm = Assembly.Load(buf);

            return this.ParseAssemblyForModule(asm);
        }
        private IScriptEngine ParseAssemblyForModule(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IScriptEngine).IsAssignableFrom(t))
                {
                    IScriptEngine manager = (IScriptEngine)Activator.CreateInstance(t);
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

            _ = Task.Run(() => _pythonLoadPlugin.Execute(job));
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            UploadTaskResponse ur = JsonSerializer.Deserialize<UploadTaskResponse>(((TestDataBroker)_messageManager).GetRecentOutput());
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
            await ((IFileModule)_pythonLoadPlugin).HandleNextMessage(responseResult);
            string response = ((TestDataBroker)_messageManager).GetRecentOutput();
            ur = JsonSerializer.Deserialize<UploadTaskResponse>(response);
            Assert.IsTrue(ur.user_output.Contains("Loaded."));
        }
        [TestMethod]
        public async Task TestStandaloneScript()
        {
            string script = @"
def main():
    print('hello from standalone')

main()
";
            var parameters = new Dictionary<string, object>
            {
                { "file", Misc.Base64Encode(script) },
                { "args", "" }
            };
            ServerJob job = new ServerJob()
            {
                task = new ServerTask()
                {
                    id = "3",
                    parameters = JsonSerializer.Serialize(parameters),
                    command = "python-exec"
                }
            };
            _ = Task.Run(() => _pythonExecPlugin.Execute(job));
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            Thread.Sleep(1000);
            string output = ((TestDataBroker)_messageManager).GetRecentOutput();
            TaskResponse rr = JsonSerializer.Deserialize<TaskResponse>(output);

            Assert.IsTrue(rr.user_output.Contains("hello from standalone"));
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
            _ = Task.Run(() => _pythonExecPlugin.Execute(job));
            ((TestDataBroker)_messageManager).hasResponse.WaitOne(TimeSpan.FromSeconds(30));
            Thread.Sleep(1000);
            string output = ((TestDataBroker)_messageManager).GetRecentOutput();
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
