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
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
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
                await messageManager.Write($"{args.path} removed.", job.task.id, true);

            }
            catch (Exception e)
            {

                await messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
    }
}
