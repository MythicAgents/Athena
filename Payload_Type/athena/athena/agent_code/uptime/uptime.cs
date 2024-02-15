using Agent.Interfaces;
using Agent.Models;
using System.Text;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "uptime";
        private IMessageManager messageManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            var Uptime64 = TimeSpan.FromMilliseconds(Environment.TickCount64);
            string UptimeD = Uptime64.Days.ToString();
            string UptimeH = Uptime64.Hours.ToString();
            string UptimeM = Uptime64.Minutes.ToString();
            string UptimeS = Uptime64.Seconds.ToString();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Current Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine(UptimeD + " Days " + UptimeH + " Hours " + UptimeM + " Mins " + UptimeS + " Seconds ");
            await messageManager.AddResponse(new TaskResponse
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = job.task.id,
            });
        }
    }
}