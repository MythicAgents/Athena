using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plugin
{
    public static class rm
    {
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    FileAttributes attr = File.GetAttributes((string)args["path"]);

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Delete((string)args["path"], true);
                    }
                    else
                    {
                        File.Delete((string)args["path"]);
                    }

                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Deleted: " + (string)args["path"],
                        task_id = (string)args["task-id"],
                    };
                }
                else
                {

                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Please specify a file to delete!",
                        task_id = (string)args["task-id"],
                        status = "error"
                    };
                }
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

    }
}
