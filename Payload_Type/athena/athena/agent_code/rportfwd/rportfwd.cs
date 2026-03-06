using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Collections.Concurrent;

namespace Workflow
{
    public class Plugin : IModule, IProxyModule
    {
        public string Name => "rportfwd";
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<int, ConnectionConfig> connections { get; set; }
        private ConcurrentDictionary<int, ConnectionConfig> clientLookup { get; set; }
        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
            this.connections = new ConcurrentDictionary<int, ConnectionConfig>();
            this.clientLookup = new ConcurrentDictionary<int, ConnectionConfig>();
            this.logger = logger;
        }

        public async Task Execute(ServerJob job)
        {
            var parameters = Misc.ConvertJsonStringToDict(job.task.parameters);

            if (!int.TryParse(parameters["lport"], out int port))
            {
                ReturnError("Failed to parse port, please use a valid numerical value.", job.task.id);
                return;
            }

            if (connections.ContainsKey(port))
            {
                ReturnError("Port in use.", job.task.id);
                return;
            }

            ConnectionConfig cc = new ConnectionConfig(port, messageManager, clientLookup);
            if (this.connections.TryAdd(port, cc))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Listening.",
                    completed = true
                });
                return;
            }

            ReturnError("Failed to start (Unknown)", job.task.id);
        }

        public async Task HandleDatagram(ServerDatagram sm)
        {
            if (clientLookup.TryGetValue(sm.server_id, out var config))
            {
                await config.HandleMessage(sm);
            }
        }

        private void ReturnError(string message, string task_id)
        {
            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = task_id,
                user_output = message,
                status = "error",
                completed = true
            });
        }
    }
}
