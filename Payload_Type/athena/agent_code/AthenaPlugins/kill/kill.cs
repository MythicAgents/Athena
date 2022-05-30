using PluginBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace Athena
{
    public static class kill
    {
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            if (!args.ContainsKey("id") || String.IsNullOrEmpty(args["id"].ToString()))
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = "ID not specified!",
                    task_id = (string)args["task-id"],
                    status = "error"
                };

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
                            return new ResponseResult
                            {
                                completed = "true",
                                user_output = "Process ID " + proc.Id + " did not exit in the alotted time.",
                                task_id = (string)args["task-id"],
                                status = "error"
                            };
                        }
                        Thread.Sleep(1000);
                        i++;
                    }

                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Process ID " + proc.Id + " killed.",
                        task_id = (string)args["task-id"],
                    };
                }
                catch (Exception e)
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = e.ToString(),
                        task_id = (string)args["task-id"],
                        status = "error"
                    };
                }
            }

        }
    }
}
