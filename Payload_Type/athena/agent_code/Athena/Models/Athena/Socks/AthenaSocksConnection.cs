using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace Athena.Models.Athena.Socks
{
    public class AthenaSocksConnection : NetCoreServer.TcpClient
    {
        public delegate void ExitRequestedHandler(object sender, SocksEventArgs e);
        public event EventHandler<SocksEventArgs> HandleSocksEvent;
        public int server_id { get; set; }
        public bool exited { get; set; }
        ConnectionOptions co { get; set; }
        CancellationTokenSource ct { get; set; }

        object _lock = new object();

        public AthenaSocksConnection(ConnectionOptions co) : base(co.ip, co.port) {
            this.server_id = co.server_id;
            this.co = co;
            this.exited = false;
            this.ct = new CancellationTokenSource();
            this.OptionReceiveBufferLimit = 65530;
            this.OptionReceiveBufferSize = 65530;
            this.OptionSendBufferLimit = 65530;
            this.OptionSendBufferSize = 65530;
        }

        public void DisconnectAndStop()
        {
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            SocksMessage smOut = new SocksMessage() //Put together our Mythic Response
            {
                server_id = this.server_id,
                data = Misc.Base64Encode(new ConnectResponse
                {
                    bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                    bndport = new byte[] { 0x00, 0x00 },
                    addrtype = co.addressType,
                    status = ConnectResponseStatus.Success,
                }.ToByte()).Result,
                exit = this.exited
            };
            HandleSocksEvent(this, new SocksEventArgs(smOut));
            this.ReceiveAsync();

        }

        protected override void OnDisconnected()
        {
            //Is there a last little bit of buffer that's getting ignored here and that's causing the SSL errors?
            //maybe this? https://github.com/chronoxor/NetCoreServer/issues/166;
            //https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.receiveasync?view=net-7.0#system-net-sockets-socket-receiveasync(system-net-sockets-socketasynceventargs)
            //.NET 7 will support AsyncSocket stuff so I might be able to migrate to that.
            this.exited = true;
            this.ct.Cancel();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            byte[] b = new byte[size];

            Array.Copy(buffer, offset, b, 0, size);


            SocksMessage smOut = new SocksMessage
            {
                server_id = this.server_id,
                data = Misc.Base64Encode(b).Result,
                exit = this.exited
            };

            HandleSocksEvent(this, new SocksEventArgs(smOut));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"TCP client caught an error with code {error}");
        }
    }
}
