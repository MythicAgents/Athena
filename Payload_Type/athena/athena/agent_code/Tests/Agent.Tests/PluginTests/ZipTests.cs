using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class ZipTests
    {
        [TestMethod]
        public async Task ZipCommandCreatesArchive()
        {
            IMessageManager messages = new TestMessageManager();
            IPlugin plugin = new PluginLoader(messages).LoadPluginFromDisk("zip");
            string source = Utilities.CreateTempDirectoryWithRandomFiles();
            string destination = Path.Combine(Path.GetTempPath(), $"athena-zip-{Guid.NewGuid():N}.7z");
            try
            {
                var job = new ServerJob
                {
                    task = new ServerTask
                    {
                        id = "zip-smoke",
                        command = "zip",
                        parameters = JsonSerializer.Serialize(new Dictionary<string, string>
                        {
                            ["source"] = source,
                            ["destination"] = destination
                        })
                    }
                };

                await plugin.Execute(job);

                Assert.IsTrue(File.Exists(destination));
                Assert.IsTrue(new FileInfo(destination).Length > 0);
                TaskResponse response = JsonSerializer.Deserialize<TaskResponse>(((TestMessageManager)messages).GetRecentOutput())!;
                Assert.IsFalse(string.Equals("error", response.status, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (File.Exists(destination)) File.Delete(destination);
                if (Directory.Exists(source)) Directory.Delete(source, true);
            }
        }
    }
}
