using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "zip";
        private IMessageManager messageManager { get; set; }
        private IAgentConfig agentConfig { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.agentConfig = config;
        }
        void DebugWriteLine(string message, string task_id)
        {
            if (agentConfig.debug)
            {
                messageManager.WriteLine(message, task_id, false);
            }
        }
        public async Task Execute(ServerJob job)
        {
            ZipArgs args = JsonSerializer.Deserialize<ZipArgs>(job.task.parameters);
            // Open a memory stream to write our zip into

            if(args == null || !args.Validate())
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to validate arguments",
                    completed = true,
                    status = "error"
                });
                return;
            }

            if (!Directory.Exists(args.source))
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Source folder doesn't exist",
                    completed = true,
                    status="error"
                });
                return;
            }

            if (File.Exists(args.destination))
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Destination already exists",
                    completed = true,
                    status = "error",
                });
                return;
            }
            ZipFile.CreateFromDirectory(args.source, args.destination, CompressionLevel.SmallestSize, false);
            await messageManager.AddResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = $"Zip written to {args.destination}.",
                completed = true
            });

            // If we have nothing to write, let's bounce
            return;
        }
    }
}
