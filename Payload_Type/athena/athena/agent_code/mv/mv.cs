
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "mv";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            if (args.ContainsKey("source") && args.ContainsKey("destination"))
            {
                try
                {
                    FileAttributes attr = File.GetAttributes((args["source"]).Replace("\"", ""));

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Move((args["source"]).Replace("\"", ""), (args["destination"]).Replace("\"", ""));
                    }
                    else
                    {
                        File.Move((args["source"]).Replace("\"", ""), (args["destination"]).Replace("\"", ""));
                    }

                    await messageManager.AddResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = $"Moved {(args["source"]).Replace("\"", "")} to {(args["destination"]).Replace("\"", "")}",
                        task_id = job.task.id,
                    });
                }
                catch (Exception e)
                {
                    messageManager.Write(e.ToString(), job.task.id, true, "error");
                    return;
                }
            }
            else
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x2B" } },
                    task_id = job.task.id,
                });
            }
        }
    }
}
