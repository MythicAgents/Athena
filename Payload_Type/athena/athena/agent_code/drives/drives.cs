using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Text;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "drives";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.logger = logger;
        }

        public async Task Execute(ServerJob job)
        {
            try
            {
                var output = DriveInfo.GetDrives();
                StringBuilder sb = new StringBuilder();
                foreach(var drive in output)
                {
                    sb.AppendLine("Name: " + drive.Name);
                }

                //string output = JsonSerializer.Serialize(DriveInfo.GetDrives());
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = sb.ToString(),
                    completed = true
                });
            }
            catch (Exception e)
            {
                logger.Log(e.ToString());
            }
        }
    }
}
