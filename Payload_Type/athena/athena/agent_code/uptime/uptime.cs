using Workflow.Contracts;
using Workflow.Models;
using System.Text;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "uptime";
        private IDataBroker messageManager { get; set; }
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var Uptime64 = TimeSpan.FromMilliseconds(Environment.TickCount64);
            string UptimeD = Uptime64.Days.ToString();
            string UptimeH = Uptime64.Hours.ToString();
            string UptimeM = Uptime64.Minutes.ToString();
            string UptimeS = Uptime64.Seconds.ToString();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Current Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine(UptimeD + " Days " + UptimeH + " Hours " + UptimeM + " Mins " + UptimeS + " Seconds ");
            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = job.task.id,
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}