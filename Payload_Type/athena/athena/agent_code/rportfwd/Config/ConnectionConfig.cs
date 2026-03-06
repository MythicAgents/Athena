using Workflow.Contracts;
using Workflow.Models;
using System.Collections.Concurrent;
using System.Net;

namespace Workflow
{
    public class ConnectionConfig
    {
        public int Port { get; set; }
        private ConcurrentDictionary<int, AsyncTcpClient> Clients;
        private ConcurrentDictionary<int, ConnectionConfig> clientLookup;
        private AsyncTcpListener server;
        private IDataBroker messageManager;

        public ConnectionConfig() { }
        public ConnectionConfig(
            int port,
            IDataBroker messageManager,
            ConcurrentDictionary<int, ConnectionConfig> clientLookup)
        {
            this.messageManager = messageManager;
            this.Port = port;
            this.clientLookup = clientLookup;
            this.Clients = new ConcurrentDictionary<int, AsyncTcpClient>();
            this.server = new AsyncTcpListener()
            {
                IPAddress = IPAddress.Any,
                Port = port,
                ClientConnectedCallback = tcpClient =>
                {
                    tcpClient.NoDelay = true;
                    return new AsyncTcpClient
                    {
                        ConnectionId = Utilities.Misc.GenerateRandomNumber(),
                        ServerTcpClient = tcpClient,
                        ConnectedCallback = ConnectedCallback,
                        ReceivedCallback = ReceivedCallback,
                        ClosedCallback = ClosedCallback,
                    }.RunAsync();
                }
            };
            _ = this.server.RunAsync();
        }
        private Task ConnectedCallback(AsyncTcpClient client, bool isReconnected)
        {
            this.Clients.TryAdd(client.ConnectionId, client);
            this.clientLookup.TryAdd(client.ConnectionId, this);
            messageManager.AddDatagram(
                DatagramSource.RPortFwd,
                new ServerDatagram(client.ConnectionId, Array.Empty<byte>(), false));
            return Task.CompletedTask;
        }
        private Task ReceivedCallback(AsyncTcpClient client, int count)
        {
            messageManager.AddDatagram(
                DatagramSource.RPortFwd,
                new ServerDatagram(
                    client.ConnectionId,
                    client.ByteBuffer.Dequeue(count),
                    false));
            return Task.CompletedTask;
        }
        private void ClosedCallback(AsyncTcpClient client, bool closedByRemote)
        {
            int id = client.ConnectionId;
            this.clientLookup.TryRemove(id, out _);
            if (this.Clients.TryRemove(id, out _))
            {
                client.Dispose();
            }
            messageManager.AddDatagram(
                DatagramSource.RPortFwd,
                new ServerDatagram(id, Array.Empty<byte>(), true));
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
            if (!this.Clients.TryGetValue(msg.server_id, out var client))
            {
                return;
            }

            if (msg.data is not null)
            {
                await client.Send(Utilities.Misc.Base64DecodeToByteArray(msg.data));
            }

            if (msg.exit)
            {
                this.clientLookup.TryRemove(msg.server_id, out _);
                if (this.Clients.TryRemove(msg.server_id, out _))
                {
                    if (client.IsConnected)
                    {
                        client.Disconnect();
                    }
                    client.Dispose();
                }
            }
        }
    }
}