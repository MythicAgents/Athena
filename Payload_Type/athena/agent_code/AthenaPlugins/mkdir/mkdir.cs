using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plugin
{
    public static class mkdir
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    DirectoryInfo dir = Directory.CreateDirectory((string)args["path"]);

                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Created directory " + dir.FullName,
                        task_id = (string)args["task-id"],
                    };
                }
                else
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Please specify a directory to create!",
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
