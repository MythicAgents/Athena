using Athena.Models.Athena.Commands;
using Athena.Models.Athena.Socks;
using Athena.Models.Mythic.Response;
using Athena.Models.Socks;
using Athena.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Athena.Commands
{
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
                    this.connections[sm.server_id].client.Send(Misc.Base64DecodeToByteArray(sm.data)); //The connection is open still, let's send the packet
                }
            }

            if (sm.exit) //Finally, let's see if exit was set to true
            {
                if (this.connections[sm.server_id].client.IsConnected) //Are we still connected and need to initiate a disconnect?
                {
                    this.connections[sm.server_id].client.Disconnect(); //We are, so let's issue the disconnect
                }
                AthenaSocksConnection ac;
                if (this.connections.TryRemove(sm.server_id, out ac))//Finally, remove the session from our tracker and dispose of the client
                {
                    ac.client.Dispose();
                }
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
                
                sc.client.RunAsync(); //Connect to the endpoint
            } 
            catch
            {
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
                            addrtype = (byte)AddressType.IPv4,
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

            foreach (var conn in this.connections)
            {
                List<SocksMessage> returnMessages = conn.Value.GetMessages();
                Console.WriteLine($"[{DateTime.Now}] Adding: {returnMessages.Count()}");
                foreach (var msg in returnMessages)
                {
                    SocksResponseHandler.AddSocksMessageAsync(msg);
                    if (msg.exit)
                    {
                        this.connections.TryRemove(msg.server_id, out _);
                    }
                }
            }
        }
    }
}