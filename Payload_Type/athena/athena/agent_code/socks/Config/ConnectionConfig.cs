using Agent.Interfaces;
using Agent.Models;
using System.Collections.Concurrent;

namespace Agent
{
    public class ConnectionConfig
    {
        public int server_id { get; set; }
        public AsyncTcpClient client;
        public AutoResetEvent onSocksEvent = new AutoResetEvent(false);
        private ConcurrentBag<ServerDatagram> messages = new ConcurrentBag<ServerDatagram>();
        public bool exited;
        private IMessageManager messageManager { get; set; }

        public ConnectionConfig(ConnectionOptions co, IMessageManager messageManager)
        {
            this.messageManager = messageManager;
            client = new AsyncTcpClient(co)
            {
                ConnectedCallback = async (client, isReconnected) =>
                {
                    ServerDatagram smOut = new ServerDatagram(
                     server_id,
                     new ConnectResponse
                     {
                         bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                         bndport = new byte[] { 0x00, 0x00 },
                         addrtype = (byte)AddressType.IPv4,
                         status = ConnectResponseStatus.Success,
                     }.ToByte(),
                     IsConnected() ? false : true
                    );
                    messageManager.AddResponse(DatagramSource.Socks5, smOut);
                    onSocksEvent.Set();
                },
                ClosedCallback = async (client, closedByRemote) =>
                {
                    onSocksEvent.Set();
                    messageManager.AddResponse(DatagramSource.Socks5, new ServerDatagram(server_id, new byte[] { }, true));
                    exited = true;
                },
                ReceivedCallback = async (client, count) =>
                {
                    byte[] buf;
                    if (count > 0)
                    {
                        if(this.client is not null)
                        {
                            buf = this.client.ByteBuffer.Dequeue(count);
                            messageManager.AddResponse(DatagramSource.Socks5, new ServerDatagram(server_id, buf, IsConnected() ? false : true));
                        }
                    }
                }
            };
            server_id = co.server_id;
            exited = false;
        }

        public bool IsConnected()
        {
            try
            {
                return client.IsConnected;
            }
            catch
            {
                return false;
            }
        }
    }
}
