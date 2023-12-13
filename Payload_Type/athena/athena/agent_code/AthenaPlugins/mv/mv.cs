using System.IO;
using System;
using System.Collections.Generic;
using Athena.Commands.Models;
using Athena.Models;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Mv : IPlugin
    {
        public string Name => "mv";

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
            if (args.ContainsKey("source") && args.ContainsKey("destination"))
            {
                try
                {
                    FileAttributes attr = File.GetAttributes((args["source"]).Replace("\"", ""));

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Move((args["source"]).Replace("\"", ""), (args["destination"]).Replace("\"", ""));
                    }
                    else
                    {
                        File.Move((args["source"]).Replace("\"", ""), (args["destination"]).Replace("\"", ""));
                    }

                    TaskResponseHandler.AddResponse(new ResponseResult
                    {
                        completed = true,
                        user_output = $"Moved {(args["source"]).Replace("\"", "")} to {(args["destination"]).Replace("\"", "")}",
                        task_id = args["task-id"],
                    });
                }
                catch (Exception e)
                {
                    TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
                    return;
                }
            }
            else
            {
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x2B" } },
                    task_id = args["task-id"],
                });
            }
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
