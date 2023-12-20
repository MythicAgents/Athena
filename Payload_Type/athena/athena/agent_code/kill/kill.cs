using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace kill
{
    public class Kill : IPlugin
    {
        public string Name => "kill";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }
        public Kill(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
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
                if (job.task.token != 0)
                {
                    tokenManager.Revert();
                }
            }
        }
    }
}
