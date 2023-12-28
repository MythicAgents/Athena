using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin, IInteractivePlugin
    {
        public string Name => "winrm";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {

            await messageManager.AddResponse(new ResponseResult()
            {
                task_id = job.task.id,
                user_output = "",
                completed = true
            });
        }

        public void Interact(InteractMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
