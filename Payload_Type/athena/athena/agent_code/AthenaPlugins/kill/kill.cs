using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Athena.Commands;

namespace Plugins
{
    public class Kill : AthenaPlugin
    {
        public override string Name => "kill";
        public override void Execute(Dictionary<string, string> args)
        {
            if (!args.ContainsKey("id") || String.IsNullOrEmpty(args["id"].ToString()))
            {
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    completed = true,
                    user_output = "ID not specified!",
                    task_id = args["task-id"],
                    status = "error"
                });

            }
            else
            {
                try
                {
                    Process proc = Process.GetProcessById(int.Parse(args["id"]));
                    proc.Kill();

                    int i = 0;
                    while (!proc.HasExited)
                    {
                        if (i == 30)
                        {
                            TaskResponseHandler.AddResponse(new ResponseResult
                            {
                                completed = true,
                                user_output = "Process ID " + proc.Id + " did not exit in the alotted time.",
                                task_id = args["task-id"],
                                status = "error"
                            });
                            return;
                        }
                        Thread.Sleep(1000);
                        i++;
                    }

                    TaskResponseHandler.AddResponse(new ResponseResult
                    {
                        completed = true,
                        user_output = "Process ID " + proc.Id + " killed.",
                        task_id = args["task-id"],
                    });
                }
                catch (Exception e)
                {
                    TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
                    return;
                }
            }
        }
    }
}
