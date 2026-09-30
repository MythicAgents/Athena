using Agent.Utilities;
using System.Security.Cryptography;
using System.Text.Json;

namespace Agent.Tests.PluginTests
{
    [TestClass]
    public class UploadTests
    {
        [TestMethod]
        public async Task UploadCommandWritesAllChunks()
        {
            const int chunkSize = 512000;
            byte[] expected = RandomNumberGenerator.GetBytes(chunkSize + 137);
            string directory = Path.Combine(Path.GetTempPath(), $"athena-upload-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string fileName = "uploaded.bin";
            string destination = Path.Combine(directory, fileName);
            IMessageManager messages = new TestMessageManager();
            IFilePlugin plugin = (IFilePlugin)new PluginLoader(messages).LoadPluginFromDisk("upload");
            var job = new ServerJob
            {
                task = new ServerTask
                {
                    id = "upload-smoke",
                    command = "upload",
                    parameters = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["path"] = directory,
                        ["filename"] = fileName,
                        ["host"] = Environment.MachineName
                    })
                }
            };
            try
            {
                await plugin.Execute(job);
                for (int chunk = 0; chunk < 2; chunk++)
                {
                    byte[] data = expected.Skip(chunk * chunkSize).Take(chunkSize).ToArray();
                    await plugin.HandleNextMessage(new ServerTaskingResponse
                    {
                        task_id = job.task.id,
                        file_id = "server-file",
                        total_chunks = 2,
                        chunk_num = chunk + 1,
                        chunk_data = Misc.Base64Encode(data)
                    });
                }

                CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(destination));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
