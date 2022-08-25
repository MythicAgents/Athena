using PluginBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace Plugin
{
    public static class kill
    {
        public static void Execute(Dictionary<string, object> args)
        {
            if (!args.ContainsKey("id") || String.IsNullOrEmpty(args["id"].ToString()))
            {
                PluginHandler.AddResponse(new ResponseResult
                {
                    completed = "true",
                    user_output = "ID not specified!",
                    task_id = (string)args["task-id"],
                    status = "error"
                });

            }
            else
            {
                try
                {
                    Process proc = Process.GetProcessById((int)args["id"]);
                    proc.Kill();

                    int i = 0;
                    while (!proc.HasExited)
                    {
                        if (i == 30)
                        {
                            PluginHandler.AddResponse(new ResponseResult
                            {
                                completed = "true",
                                user_output = "Process ID " + proc.Id + " did not exit in the alotted time.",
                                task_id = (string)args["task-id"],
                                status = "error"
                            });
                            return;
                        }
                        Thread.Sleep(1000);
                        i++;
                    }

                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = "Process ID " + proc.Id + " killed.",
                        task_id = (string)args["task-id"],
                    });
                }
                catch (Exception e)
                {
                    PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
                    return;
                }
            }

        }
    }
}
