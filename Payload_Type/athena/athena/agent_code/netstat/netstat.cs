using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Text;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "netstat";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Proto Local Address Foreign Address State PID");
            foreach (TcpRow tcpRow in ManagedIpHelper.GetExtendedTcpTable(true))
            {
                sb.AppendFormat(" {0,-7}{1,-23}{2, -23}{3,-14}{4}", "TCP", tcpRow.LocalEndPoint, tcpRow.RemoteEndPoint, tcpRow.State, tcpRow.ProcessId);
                sb.AppendLine();
            }

                await messageManager.AddResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = sb.ToString(),
                completed = true
            });
        }
    }
}
