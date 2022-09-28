using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "mkdir";
        public override void Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    DirectoryInfo dir = Directory.CreateDirectory(((string)args["path"]).Replace("\"", ""));

                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = "Created directory " + dir.FullName,
                        task_id = (string)args["task-id"],
                    });
                }
                else
                {
                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = "Please specify a directory to create!",
                        task_id = (string)args["task-id"],
                        status = "error"
                    });
                }
            }
            catch (Exception e)
            {
                PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
                return;
            }
        }
    }
}
