using System.IO;
using System;
using System.Collections.Generic;
using PluginBase;

namespace Plugin
{
    public static class mv
    {
        public static ResponseResult Execute(Dictionary<string, object> args)
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

                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = $"Moved {((string)args["source"]).Replace("\"", "")} to {((string)args["destination"]).Replace("\"", "")}",
                        task_id = (string)args["task-id"],
                    };
                }
                catch (Exception e)
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = e.Message,
                        task_id = (string)args["task-id"],
                        status = "error"
                    };
                }
            }
            else
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = "Please specify both a source and destination for the file!",
                    task_id = (string)args["task-id"],
                };
            }
        }
    }
}
