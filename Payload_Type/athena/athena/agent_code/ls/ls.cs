using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Globalization;

using Agent.Interfaces;
using LsUtilities;
using Agent.Utilities;
using Agent.Models;

namespace Plugins
{
    public class Ls : IPlugin
    {

        public string Name => "ls";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Ls(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
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

