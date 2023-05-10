using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
using Athena.Commands;

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

                    TaskResponseHandler.AddResponse(new ResponseResult
                    {
                        completed = true,
                        user_output = "Deleted: " + (args["path"]).Replace("\"", ""),
                        task_id = args["task-id"],
                    });
                }
                else
                {

                    TaskResponseHandler.AddResponse(new ResponseResult
                    {
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x27" } },
                        task_id = args["task-id"],
                        status = "error"
                    });
                }
            }
            catch (Exception e)
            {

                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
                return;
            }
        }
    }
}
