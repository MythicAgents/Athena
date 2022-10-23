using System.IO;
using System;
using System.Collections.Generic;
using Athena.Plugins;
using Athena.Models;

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

                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = $"Moved {(args["source"]).Replace("\"", "")} to {(args["destination"]).Replace("\"", "")}",
                        task_id = args["task-id"],
                    });
                }
                catch (Exception e)
                {
                    PluginHandler.Write(e.ToString(), args["task-id"], true, "error");
                    return;
                }
            }
            else
            {
                PluginHandler.AddResponse(new ResponseResult
                {
                    completed = "true",
                    user_output = "Please specify both a source and destination for the file!",
                    task_id = args["task-id"],
                });
            }
        }
    }
}
