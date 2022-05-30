using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Athena
{
    public static class tail
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            if (!args.ContainsKey("path") || string.IsNullOrEmpty(args["path"].ToString()))
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = "Pleace specify a path!",
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
            string path = args["path"].ToString();
            int lines = 5;
            if (args.ContainsKey("lines"))
            {
                try
                {
                    lines = (int)args["lines"];
                }
                catch
                {
                    lines = 5;
                }
            }
            try
            {
                List<string> text = File.ReadLines(path).Reverse().Take(lines).ToList();
                text.Reverse();

                return new ResponseResult
                {
                    completed = "true",
                    user_output = string.Join(Environment.NewLine, text),
                    task_id = (string)args["task-id"],
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = "",
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }
    }
}
