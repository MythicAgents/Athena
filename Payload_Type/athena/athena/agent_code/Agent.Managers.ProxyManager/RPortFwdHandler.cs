using Agent.Managers;
using Agent.Models.Tasks;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using Agent.Models.Proxy;
using Agent.Interfaces;

namespace Agent.Managers
{
    public class RPortFwdHandler
    {
        //Track the number of listeners we have
        //private ConcurrentDictionary<int, AthenaTcpServer> connections { get; set; }
        private ConcurrentDictionary<int, AthenaTcpServer> connections { get; set; }
        private ConcurrentBag<MythicDatagram> messages = new ConcurrentBag<MythicDatagram>();
        public IMessageManager messageManager { get; set; }

        public RPortFwdHandler(IMessageManager messageManager)
        {
            connections = new ConcurrentDictionary<int, AthenaTcpServer>();
            this.messageManager = messageManager;
        }

        public async Task<bool> StartListener(MythicJob job)
        {
            var parameters = Misc.ConvertJsonStringToDict(job.task.parameters);
            int port;
            if (int.TryParse(parameters["lport"], out port))
            {
                if (connections.ContainsKey(port))
                {
                    return false;
                }

                AthenaTcpServer ats = new AthenaTcpServer(port);
                return connections.TryAdd(port, ats);

            }
            return false;
        }
        public async Task<bool> StopListener(int port)
        {
            foreach(var conn in connections)
            {
                if (conn.Value.Port == port)
                {
                    conn.Value.Stop();
                    return connections.TryRemove(port, out _);
                }
            }
            return false;
        }

        /// <summary>
        /// Handle a new message forwarded from the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public async Task HandleMessage(MythicDatagram sm)
        {
            foreach(var connection in this.connections)
            {
                if (connection.Value.HasClient(sm.server_id)){
                    connection.Value.HandleMessage(sm);
                    break;
                }
            }
        }

        public void GetRportFwdMessages()
        {
            if (connections.Count < 1)
            {
                return;
            }

            foreach (var conn in connections)
            {
                List<MythicDatagram> returnMessages = conn.Value.GetMessages();
                foreach (var msg in returnMessages)
                {
                    messageManager.AddProxyMessageAsync(DatagramSource.RPortFwd, msg);
                }
            }
        }
    }
}
