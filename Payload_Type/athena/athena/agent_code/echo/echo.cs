using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IInteractivePlugin, IPlugin
    {
        public string Name => "echo";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            messageManager.AddInteractMessage(new InteractMessage()
            {
                task_id = job.task.id,
                data = Misc.Base64Encode("Ready to echo"),
                message_type = InteractiveMessageType.Output,
            });
        }

        public async void Interact(InteractMessage message)
        {
            messageManager.AddInteractMessage(new InteractMessage()
            {
                task_id = message.task_id,
                data = message.data,
                message_type = InteractiveMessageType.Output,
            });
        }
    }
}
