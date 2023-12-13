using Athena.Commands;
using Athena.Commands.Models;
using Athena.Models.Comms.Tasks;
using Athena.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Athena.Models.Responses;
using System.Globalization;
using LsUtilities;

namespace Plugins
{
    public class Ls : IPlugin
    {
        public string Name => "ls";
        public bool Interactive => false;

        public bool Running = false;
        public void Start(Dictionary<string, string> args)
        {
            if (args["path"].Contains(":")) //If the path contains a colon, it's likely a windows path and not UNC
            {
                if (args["path"].Split('\\').Count() == 1) //It's a root dir and didn't include a \
                {
                    args["path"] = args["path"] + "\\";
                }

                TaskResponseHandler.AddResponse(LocalListing.GetLocalListing(args["path"], args["task-id"]));

                //TaskResponseHandler.AddResponse(ReturnLocalListing(args["path"], args["task-id"]));
            }
            else //It could be a local *nix path or a remote UNC
            {
                if (args["host"].Equals(Dns.GetHostName(), StringComparison.OrdinalIgnoreCase)) //If it's the same name as the current host
                {
                    Console.WriteLine("Host is the same as our DNS name");
                    TaskResponseHandler.AddResponse(LocalListing.GetLocalListing(args["path"], args["task-id"]));
                }
                else //UNC Host
                {
                    Console.WriteLine("Getting remote host");
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
                    TaskResponseHandler.AddResponse(RemoteListing.GetRemoteListing(fullPath, host, args["task-id"]));
                }
            }
        }
        public void Interact(InteractiveMessage message)
        {

        }
        public void Stop(string task_id)
        {

        }
        public bool IsRunning()
        {
            return this.Running;
        }
    }
}
