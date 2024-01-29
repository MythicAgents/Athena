using Agent.Interfaces;
using Agent.Models;
using System.Collections.Concurrent;
using System.Net;

namespace Agent
{
    public class ConnectionConfig
    {
        public int Port { get; set; }
        private Dictionary<int, AsyncTcpClient> Clients;
        private ConcurrentBag<ServerDatagram> messages = new ConcurrentBag<ServerDatagram>();
        private AsyncTcpListener server;
        private IMessageManager messageManager;

        public ConnectionConfig() { }
        public ConnectionConfig(int port, IMessageManager messageManager)
        {
            this.messageManager = messageManager;
            this.Port = port;
            this.server = new AsyncTcpListener()
            {
                IPAddress = IPAddress.Any,
                Port = port,
                ClientConnectedCallback = tcpClient => new AsyncTcpClient
                {
                    ConnectionId = Utilities.Misc.GenerateRandomNumber(),
                    ServerTcpClient = tcpClient,
                    ConnectedCallback = ConnectedCallback,
                    ReceivedCallback = ReceivedCallback,
                    ClosedCallback = ClosedCallback,

                }.RunAsync()
            };
            this.Clients = new Dictionary<int, AsyncTcpClient>();
            this.server.RunAsync();
        }
        private async Task ConnectedCallback(AsyncTcpClient client, bool isReconnected)
        {
            //Add Client to our tracker
            this.Clients.Add(client.ConnectionId, client);
            await messageManager.AddResponse(DatagramSource.RPortFwd,new ServerDatagram(client.ConnectionId, new byte[] { }, false));
        }
        private async Task ReceivedCallback(AsyncTcpClient client, int count)
        {
            await messageManager.AddResponse(DatagramSource.RPortFwd, new ServerDatagram(client.ConnectionId, client.ByteBuffer.Dequeue(count), client.IsConnected ? false : true));
        }
        private void ClosedCallback(AsyncTcpClient client, bool closedByRemote)
        {
            //Remove client
            this.Clients.Remove(client.Port);
            client.Dispose();
            messageManager.AddResponse(DatagramSource.RPortFwd, new ServerDatagram(client.ConnectionId, new byte[] { }, true));
        }

        public bool HasClient(int id)
        {
            return this.Clients.ContainsKey(id);
        }

        public void Stop()
        {
            this.server.Stop(true);
        }


        public async Task HandleMessage(ServerDatagram msg)
        {
            if (this.Clients.ContainsKey(msg.server_id))
            {
                try
                {
                    if(msg.data is not null)
                    {
                        await this.Clients[msg.server_id].Send(Utilities.Misc.Base64DecodeToByteArray(msg.data));
                    }

                    if (msg.exit)
                    {
                        if (this.Clients[msg.server_id].IsConnected)
                        {
                            this.Clients[msg.server_id].Disconnect();
                            this.Clients[msg.server_id].Dispose();
                        }
                    }
                }
                catch { }
            }
        }

    }
}