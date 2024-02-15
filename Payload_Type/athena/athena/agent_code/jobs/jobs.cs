using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "jobs";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, ServerJob> jobs = messageManager.GetJobs();
            Dictionary<string, string> jobsOut = jobs.ToDictionary(j => j.Value.task.id, j => j.Value.task.command);

            await messageManager.AddResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = JsonSerializer.Serialize(jobsOut),
                completed = true
            });
        }
    }
}
