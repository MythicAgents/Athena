using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;
using zip_inspect;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "zip-inspect";
        private IMessageManager messageManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            StringBuilder output = new StringBuilder();
            ZipInspectArgs args = JsonSerializer.Deserialize<ZipInspectArgs>(job.task.parameters);
            if (args is null){
                return;
            }
            FileInfo fInfo = new FileInfo(args.path);
            if (!fInfo.Exists)
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    user_output = $"Zipfile does not exist: {args.path}",
                    task_id = job.task.id,
                });
                return;
            }

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(args.path))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        output.AppendLine($"{entry.FullName} {entry.Name} {entry.Length} {entry.CompressedLength} {entry.IsEncrypted}");
                    }
                }
            }
            catch (Exception e)
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    user_output = e.ToString(),
                    task_id = job.task.id,
                    status = "error"
                });
                return;
            }

            await messageManager.AddResponse(new TaskResponse
            {
                completed = true,
                user_output = output.ToString(),
                task_id = job.task.id,
            });

        }
    }
}
