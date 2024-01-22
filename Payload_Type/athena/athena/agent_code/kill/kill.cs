using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using kill;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "kill";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            KillArgs args = JsonSerializer.Deserialize<KillArgs>(job.task.parameters);

            if(args.id < 1)
            {
                await messageManager.AddResponse(new ResponseResult
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x26" } },
                    task_id = job.task.id,
                    status = "error"
                });
            }

            try
            {
                using (var proc = Process.GetProcessById(args.id))
                {
                    proc.Kill(args.tree);
                    await proc.WaitForExitAsync();

                    await messageManager.AddResponse(new ResponseResult
                    {
                        completed = true,
                        user_output = "Process ID " + proc.Id + " killed.",
                        task_id = job.task.id,
                    });
                }
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
    }
}
