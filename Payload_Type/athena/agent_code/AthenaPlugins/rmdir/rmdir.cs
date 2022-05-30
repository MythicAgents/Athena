using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;

namespace Athena
{
    public static class rmdir
    {
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("directory"))
                {
                    if(args.ContainsKey("force") && (string)args["force"] == "true")
                    {
                        Directory.Delete((string)args["directory"],true);

                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Deleted Directory and all sub files and folders in: " + (string)args["directory"],
                            task_id = (string)args["task-id"],
                        };
                    }
                    else
                    {
                        Directory.Delete((string)args["directory"]);

                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Deleted Directory: " + (string)args["directory"],
                            task_id = (string)args["task-id"],
                        };
                    }
                }
                else
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Please specify a directory to delete.",
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
