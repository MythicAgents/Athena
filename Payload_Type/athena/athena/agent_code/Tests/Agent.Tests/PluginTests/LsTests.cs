using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class LsTests
    {
        [TestMethod]
        public async Task LsCommandListsDirectoryContents()
        {
            IMessageManager messages = new TestMessageManager();
            IPlugin plugin = new PluginLoader(messages).LoadPluginFromDisk("ls");
            string directory = Path.Combine(Path.GetTempPath(), $"athena-ls-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string expectedName = "known-file.txt";
            await File.WriteAllTextAsync(Path.Combine(directory, expectedName), "known contents");
            try
            {
                var job = new ServerJob
                {
                    task = new ServerTask
                    {
                        id = "ls-smoke",
                        command = "ls",
                        parameters = JsonSerializer.Serialize(new Dictionary<string, string> { ["path"] = directory })
                    }
                };

                await plugin.Execute(job);

                FileBrowserTaskResponse response = JsonSerializer.Deserialize<FileBrowserTaskResponse>(((TestMessageManager)messages).GetRecentOutput())!;
                Assert.IsTrue(response.file_browser.files.Any(file => file.name == expectedName));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
