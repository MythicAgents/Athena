using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;

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

            if(args.id < 1 && string.IsNullOrEmpty(args.name))
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "No ID or name specified.",
                    task_id = job.task.id,
                    status = "error"
                });
            }
            
            if(args.id > 0)
            {
                await KillById(args, job.task.id);
            }
            else
            {
                await KillByName(args.name, job.task.id);
            }
        }
        public async Task KillByName(string name, string task_id)
        {
            StringBuilder sb = new StringBuilder();
            Process[] processes = Process.GetProcessesByName(name);

            if(processes.Length == 0)
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "No processes found.",
                    task_id = task_id,
                });
                return;
            }

            Parallel.ForEach(processes, proc =>
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit();
                    sb.AppendLine(proc.Id+ ": exited.");
                }
                catch (Exception e)
                {
                    sb.AppendLine(proc.Id+ ": " + e);
                }
            });

            await messageManager.AddResponse(new TaskResponse
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = task_id,
            });

        }
        public async Task KillById(KillArgs args, string task_id)
        {
            using (var proc = Process.GetProcessById(args.id))
            {
                proc.Kill(args.tree);
                await proc.WaitForExitAsync();

                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Process ID " + proc.Id + " killed.",
                    task_id = task_id,
                });
            }
        }
    }
}
