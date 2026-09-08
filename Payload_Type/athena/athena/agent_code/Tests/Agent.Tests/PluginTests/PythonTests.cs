using Agent.Utilities;
using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class PythonTests
    {
        [TestMethod]
        public async Task PythonCommandExecutesScriptWithArguments()
        {
            IMessageManager messages = new TestMessageManager();
            IPlugin plugin = new PluginLoader(messages).LoadPluginFromDisk("python-exec");
            const string script = "import sys\nprint('|'.join(sys.argv))";
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "python-smoke",
                    command = "python-exec",
                    parameters = JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        ["file"] = Misc.Base64Encode(script),
                        ["args"] = "alpha \"two words\""
                    })
                }
            };

            await plugin.Execute(job);
            ((TestMessageManager)messages).hasResponse.WaitOne(TimeSpan.FromSeconds(5));

            TaskResponse response = JsonSerializer.Deserialize<TaskResponse>(((TestMessageManager)messages).GetRecentOutput())!;
            StringAssert.Contains(response.user_output, "alpha");
            StringAssert.Contains(response.user_output, "two words");
        }
    }
}
