using Athena.Models;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Kill : IPlugin
    {
        public string Name => "kill";

        public bool Interactive => false;

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public void Start(Dictionary<string, string> args)
        {
            if (!args.ContainsKey("id") || String.IsNullOrEmpty(args["id"].ToString()))
            {
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x26" } },
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

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }
    }
}
