using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class CatTests
    {
        [TestMethod]
        public async Task CatCommandReadsFileContents()
        {
            IMessageManager messages = new TestMessageManager();
            IPlugin plugin = new PluginLoader(messages).LoadPluginFromDisk("cat");
            string path = Path.GetTempFileName();
            const string expected = "athena cat smoke test";
            await File.WriteAllTextAsync(path, expected);
            try
            {
                var job = new ServerJob
                {
                    task = new ServerTask
                    {
                        id = "cat-smoke",
                        command = "cat",
                        parameters = JsonSerializer.Serialize(new Dictionary<string, string> { ["path"] = path })
                    }
                };

                await plugin.Execute(job);

                TaskResponse response = JsonSerializer.Deserialize<TaskResponse>(((TestMessageManager)messages).GetRecentOutput())!;
                Assert.AreEqual(expected, response.user_output);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
