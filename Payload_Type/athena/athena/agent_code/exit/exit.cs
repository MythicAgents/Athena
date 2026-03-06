using Workflow.Contracts;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "exit";
        private IDataBroker messageManager { get; set; }

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }

#pragma warning disable CS1998
        public async Task Execute(ServerJob job)
#pragma warning restore CS1998
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            Environment.Exit(0);
        }
    }
}
