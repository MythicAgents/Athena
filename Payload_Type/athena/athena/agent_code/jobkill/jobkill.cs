using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "jobkill";
        private IDataBroker messageManager { get; set; }

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {

            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            ServerJob jobToKill;

            if (!this.messageManager.TryGetJob(args["id"], out jobToKill)){
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Job not found.",
                    completed = true
                });
                return;
            }

            jobToKill.cancellationtokensource.Cancel();
            
            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = "Cancellation request sent.",
                completed = true
            });
        }
    }
}
