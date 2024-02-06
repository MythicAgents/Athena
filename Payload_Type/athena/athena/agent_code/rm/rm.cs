using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using rm;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "rm";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            RmArgs args = JsonSerializer.Deserialize<RmArgs>(job.task.parameters);

            if(!args.Validate(out string message))
            {
                await messageManager.Write(message, job.task.id, true, "error");
                return;
            }   

            try
            {
                FileAttributes attr = File.GetAttributes(args.path);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    Directory.Delete(args.path.Replace("\"", ""), true);
                }
                else
                {
                    File.Delete(args.path.Replace("\"", ""));
                }
                messageManager.Write($"{args.path} removed.", job.task.id, true);

            }
            catch (Exception e)
            {

                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
    }
}
