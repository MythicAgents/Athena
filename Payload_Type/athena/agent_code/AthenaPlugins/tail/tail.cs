using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Plugin
{
    public static class tail
    {

        public static void Execute(Dictionary<string, object> args)
        {
            if (!args.ContainsKey("path") || string.IsNullOrEmpty(args["path"].ToString()))
            {
                PluginHandler.WriteOutput("Please specify a path!", (string)args["task-id"], true, "error");
                return;
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

                PluginHandler.AddResponse(new ResponseResult
                {
                    completed = "true",
                    user_output = string.Join(Environment.NewLine, text),
                    task_id = (string)args["task-id"],
                });
            }
            catch (Exception e)
            {
                PluginHandler.WriteOutput(e.ToString(), (string)args["task-id"], true, "error");
            }
        }
    }
}
