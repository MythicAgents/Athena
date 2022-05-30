using System;
using System.Collections.Generic;
using System.IO;
using PluginBase;

namespace Athena
{
    public static class cat
    {
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = File.ReadAllText(args["path"].ToString().Replace("\"", "")),
                        task_id = (string)args["task-id"],
                    };
                }
                else
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Missing path parameter.",
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

