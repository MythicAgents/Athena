using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IInteractivePlugin, IPlugin
    {
        public string Name => "echo";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            string output = "Ready to echo:";
            await messageManager.AddResponse(new InteractMessage()
            {
                task_id = job.task.id,
                data = Misc.Base64Encode("Ready to echo"),
                message_type = InteractiveMessageType.Output,
            });


            //await messageManager.AddResponse(new ResponseResult()
            //{
            //    task_id = job.task.id,
            //    user_output = output,
            //    completed = false
            //});
        }

        public async void Interact(InteractMessage message)
        {
            string output = $"[{message.message_type}] {Misc.Base64Decode(message.data)}";


            await messageManager.AddResponse(new ResponseResult()
            {
                task_id = message.task_id,
                user_output = output,
                completed = true
            });
        }
    }
}
