using System;
using System.Collections.Generic;
using System.IO;
using PluginBase;

namespace Athena
{
    public static class cd
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    Directory.SetCurrentDirectory((string)args["path"]);
                    
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = $"Changed directory to {Directory.GetCurrentDirectory()}",
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
