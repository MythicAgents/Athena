using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "cd";
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
            try
            {
                if (args.ContainsKey("path") && !string.IsNullOrEmpty(args["path"]))
                {
                    string path = (args["path"]).Replace("\"", "");

                    Directory.SetCurrentDirectory(path);

                    messageManager.Write($"Changed directory to {Directory.GetCurrentDirectory()}", job.task.id, true);
                }
                else
                {
                    messageManager.Write("Missing path parameter", job.task.id, true, "error");
                }
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
