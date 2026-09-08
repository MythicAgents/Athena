using Agent.Utilities;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class DownloadTests
    {
        [TestMethod]
        public async Task DownloadCommandStreamsFileContents()
        {
            var config = new TestAgentConfig { chunk_size = 512000 };
            IMessageManager messages = new TestMessageManager();
            IFilePlugin plugin = (IFilePlugin)new PluginLoader(messages).LoadPluginFromDisk("download");
            string path = Path.GetTempFileName();
            byte[] expected = RandomNumberGenerator.GetBytes(config.chunk_size + 137);
            await File.WriteAllBytesAsync(path, expected);
            try
            {
                var job = new ServerJob
                {
                    task = new ServerTask
                    {
                        id = "download-smoke",
                        command = "download",
                        parameters = JsonSerializer.Serialize(new Dictionary<string, string>
                        {
                            ["host"] = Dns.GetHostName(),
                            ["path"] = path
                        })
                    }
                };

                await plugin.Execute(job);
                DownloadTaskResponse first = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)messages).GetRecentOutput())!;
                Assert.AreEqual(2, first.download.total_chunks);
                var actual = new List<byte>();
                for (int chunk = 1; chunk <= 2; chunk++)
                {
                    await plugin.HandleNextMessage(new ServerTaskingResponse
                    {
                        task_id = job.task.id,
                        file_id = "server-file",
                        total_chunks = 2,
                        chunk_num = chunk,
                        status = "success"
                    });
                    DownloadTaskResponse response = JsonSerializer.Deserialize<DownloadTaskResponse>(((TestMessageManager)messages).GetRecentOutput())!;
                    actual.AddRange(Misc.Base64DecodeToByteArray(response.download.chunk_data));
                }

                CollectionAssert.AreEqual(expected, actual.ToArray());
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
