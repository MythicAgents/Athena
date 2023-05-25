using Athena.Models.Proxy;
using System.Collections.Concurrent;
using System.Net;

namespace Athena.Handler.Proxy
{
    public class AthenaTcpServer
    {
        public int Port { get; set; }
        private Dictionary<int, AsyncTcpClient> Clients;
        private ConcurrentBag<MythicDatagram> messages = new ConcurrentBag<MythicDatagram>();
        private AsyncTcpListener server;


        public AthenaTcpServer() { }
        public AthenaTcpServer(int port)
        {
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
            this.server.RunAsync();
            this.Clients = new Dictionary<int, AsyncTcpClient>();
        }
        private async Task ConnectedCallback(AsyncTcpClient client, bool isReconnected)
        {
            //Add Client to our tracker
            this.Clients.Add(client.ConnectionId, client);
            messages.Add(new MythicDatagram(client.ConnectionId, new byte[] { }, false));
        }
        private async Task ReceivedCallback(AsyncTcpClient client, int count)
        {
            var dg = new MythicDatagram(client.ConnectionId, client.ByteBuffer.Dequeue(count), client.IsConnected ? false : true);
            messages.Add(dg);
        }
        private void ClosedCallback(AsyncTcpClient client, bool closedByRemote)
        {
            //Remove client
            this.Clients.Remove(client.Port);
            client.Dispose();
            messages.Add(new MythicDatagram(client.ConnectionId, new byte[] { }, true));
        }

        public List<MythicDatagram> GetMessages()
        {
            List<MythicDatagram> msgs = new List<MythicDatagram>(this.messages);
            this.messages.Clear();

            foreach (var msg in msgs)
            {
                msg.PrepareMessage();
            }

            return msgs;
        }

        public bool HasClient(int id)
        {
            return this.Clients.ContainsKey(id);
        }

        public void Stop()
        {
            this.server.Stop(true);
        }


        public void HandleMessage(MythicDatagram msg)
        {
            if(this.Clients.ContainsKey(msg.server_id))
            {
                try
                {
                    this.Clients[msg.server_id].Send(Utilities.Misc.Base64DecodeToByteArray(msg.data));
                    if (msg.exit)
                    {
                        this.Clients[msg.server_id].Disconnect();
                        this.Clients[msg.server_id].Dispose();
                    }
                }
                catch { }
            }
        }

    }
}
