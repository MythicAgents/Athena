using Agent.Interfaces;
using Agent.Models;
using System.Collections.Concurrent;
using System.Net;

namespace Agent
{
    public class ConnectionConfig
    {
        public const int DefaultMaxActiveClients = 128;
        public const int DefaultMaximumPendingInboundDatagrams = 64;
        public const int DefaultMaximumPendingInboundBytes = 1024 * 1024;
        public static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(30);
        public int Port { get; set; }
        private ConcurrentDictionary<int, AsyncTcpClient> Clients;
        private readonly ConcurrentDictionary<AsyncTcpClient, InboundQueue> inboundQueues = new();
        private ConcurrentBag<ServerDatagram> messages = new ConcurrentBag<ServerDatagram>();
        private AsyncTcpListener server;
        private IMessageManager messageManager;
        private readonly int maxActiveClients;
        private readonly int maximumPendingInboundDatagrams;
        private readonly int maximumPendingInboundBytes;
        private readonly Func<int, ConnectionConfig, bool> tryRegisterClientId;
        private readonly Action<int, ConnectionConfig> unregisterClientId;
        private readonly TimeSpan sendTimeout;
        private int activeClientCount;

        public ConnectionConfig(
            int maxActiveClients = DefaultMaxActiveClients,
            int maximumPendingInboundDatagrams = DefaultMaximumPendingInboundDatagrams,
            int maximumPendingInboundBytes = DefaultMaximumPendingInboundBytes,
            Func<int, ConnectionConfig, bool>? tryRegisterClientId = null,
            Action<int, ConnectionConfig>? unregisterClientId = null,
            TimeSpan? sendTimeout = null)
        {
            if (maxActiveClients <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxActiveClients));
            if (maximumPendingInboundDatagrams <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumPendingInboundDatagrams));
            if (maximumPendingInboundBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumPendingInboundBytes));
            if (sendTimeout is { } timeout && timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(sendTimeout));

            this.maxActiveClients = maxActiveClients;
            this.maximumPendingInboundDatagrams = maximumPendingInboundDatagrams;
            this.maximumPendingInboundBytes = maximumPendingInboundBytes;
            this.tryRegisterClientId = tryRegisterClientId ?? ((_, _) => true);
            this.unregisterClientId = unregisterClientId ?? ((_, _) => { });
            this.sendTimeout = sendTimeout ?? DefaultSendTimeout;
            this.Clients = new ConcurrentDictionary<int, AsyncTcpClient>();
        }

        public ConnectionConfig(
            int port,
            IMessageManager messageManager,
            int maxActiveClients = DefaultMaxActiveClients,
            int maximumPendingInboundDatagrams = DefaultMaximumPendingInboundDatagrams,
            int maximumPendingInboundBytes = DefaultMaximumPendingInboundBytes,
            Func<int, ConnectionConfig, bool>? tryRegisterClientId = null,
            Action<int, ConnectionConfig>? unregisterClientId = null,
            TimeSpan? sendTimeout = null)
            : this(
                maxActiveClients,
                maximumPendingInboundDatagrams,
                maximumPendingInboundBytes,
                tryRegisterClientId,
                unregisterClientId,
                sendTimeout)
        {
            this.messageManager = messageManager;
            this.Port = port;
            this.server = new AsyncTcpListener()
            {
                IPAddress = IPAddress.Any,
                Port = port,
                ClientConnectedCallback = tcpClient => new AsyncTcpClient
                {
                    ConnectionId = Utilities.Misc.GenerateRandomNumber(),
                    ServerTcpClient = tcpClient,
                    ConnectedCallback = ConnectedCallback,
                    ReceivedCallback = ReceivedCallback,
                    ClosedCallback = ClosedCallback,

                }.RunAsync()
            };
        }

        public Task StartAsync() => this.server.StartAsync();
        private async Task ConnectedCallback(AsyncTcpClient client, bool isReconnected)
        {
            if (!TryAcquireClientCapacity())
            {
                client.Dispose();
                return;
            }

            if (!this.Clients.TryAdd(client.ConnectionId, client))
            {
                try
                {
                    client.Dispose();
                }
                finally
                {
                    ReleaseClientCapacity();
                }
                return;
            }

            if (!tryRegisterClientId(client.ConnectionId, this))
            {
                CloseClient(client, notifyRemote: false);
                return;
            }

            inboundQueues.TryAdd(client, new InboundQueue(
                ProcessMessage,
                maximumPendingInboundDatagrams,
                maximumPendingInboundBytes));

            if (!messageManager.TryAddDatagram(
                    DatagramSource.RPortFwd,
                    new ServerDatagram(client.ConnectionId, Array.Empty<byte>(), false)))
                CloseClient(client, notifyRemote: false);
        }
        private Task ReceivedCallback(AsyncTcpClient client, int count)
        {
            byte[] bytes = client.ByteBuffer.Dequeue(count);
            bool isTerminal = !client.IsConnected;
            bool queued = messageManager.TryAddDatagram(
                DatagramSource.RPortFwd,
                new ServerDatagram(client.ConnectionId, bytes, isTerminal));

            if (isTerminal)
                CloseClient(client, notifyRemote: false);
            else if (!queued)
                CloseClient(client, notifyRemote: true);
            return Task.CompletedTask;
        }
        private void ClosedCallback(AsyncTcpClient client, bool closedByRemote)
        {
            CloseClient(client, notifyRemote: true);
        }

        private void CloseClient(AsyncTcpClient client, bool notifyRemote)
        {
            if (!this.Clients.TryRemove(
                new KeyValuePair<int, AsyncTcpClient>(client.ConnectionId, client))) return;

            if (inboundQueues.TryRemove(client, out InboundQueue? inboundQueue))
                inboundQueue.Retire();
            unregisterClientId(client.ConnectionId, this);

            try
            {
                client.Dispose();
            }
            finally
            {
                ReleaseClientCapacity();
            }
            if (notifyRemote)
                messageManager.TryAddDatagram(
                    DatagramSource.RPortFwd,
                    new ServerDatagram(client.ConnectionId, Array.Empty<byte>(), true));
        }

        private bool TryAcquireClientCapacity()
        {
            while (true)
            {
                int current = Volatile.Read(ref activeClientCount);
                if (current >= maxActiveClients) return false;
                if (Interlocked.CompareExchange(ref activeClientCount, current + 1, current) == current)
                    return true;
            }
        }

        private void ReleaseClientCapacity()
        {
            while (true)
            {
                int current = Volatile.Read(ref activeClientCount);
                if (current == 0) return;
                if (Interlocked.CompareExchange(ref activeClientCount, current - 1, current) == current)
                    return;
            }
        }

        public bool HasClient(int id)
        {
            return this.Clients.ContainsKey(id);
        }

        public void Stop()
        {
            this.server.Stop(true);
        }

        public Task StopAsync() => this.server.StopAsync(true);


        public Task HandleMessage(ServerDatagram msg)
        {
            if (this.Clients.TryGetValue(msg.server_id, out AsyncTcpClient? client) &&
                inboundQueues.TryGetValue(client, out InboundQueue? inboundQueue))
            {
                if (inboundQueue.TryEnqueue(client, msg, out Task completion))
                    return completion;

                CloseClient(client, notifyRemote: !msg.exit);
            }

            return Task.CompletedTask;
        }

        private async Task ProcessMessage(AsyncTcpClient client, ServerDatagram msg)
        {
            if (!this.Clients.TryGetValue(msg.server_id, out AsyncTcpClient? current) ||
                !ReferenceEquals(current, client)) return;

            try
            {
                if (msg.data is not null)
                {
                    using var timeout = new CancellationTokenSource(sendTimeout);
                    await client.Send(
                        Utilities.Misc.Base64DecodeToByteArray(msg.data),
                        timeout.Token);
                }

                if (msg.exit)
                {
                    if (client.IsConnected)
                        client.Disconnect();
                    CloseClient(client, notifyRemote: false);
                }
            }
            catch
            {
                CloseClient(client, notifyRemote: !msg.exit);
            }
        }

        private sealed class InboundQueue
        {
            private readonly object sync = new();
            private readonly Func<AsyncTcpClient, ServerDatagram, Task> handler;
            private readonly int maximumPendingDatagrams;
            private readonly int maximumPendingBytes;
            private Task tail = Task.CompletedTask;
            private int pendingDatagrams;
            private int pendingBytes;
            private bool retired;

            public InboundQueue(
                Func<AsyncTcpClient, ServerDatagram, Task> handler,
                int maximumPendingDatagrams,
                int maximumPendingBytes)
            {
                this.handler = handler;
                this.maximumPendingDatagrams = maximumPendingDatagrams;
                this.maximumPendingBytes = maximumPendingBytes;
            }

            public bool TryEnqueue(
                AsyncTcpClient client,
                ServerDatagram datagram,
                out Task completion)
            {
                int datagramBytes = datagram.bdata?.Length ?? datagram.data?.Length ?? 0;
                lock (sync)
                {
                    if (retired ||
                        pendingDatagrams >= maximumPendingDatagrams ||
                        datagramBytes > maximumPendingBytes - pendingBytes)
                    {
                        completion = Task.CompletedTask;
                        return false;
                    }

                    pendingDatagrams++;
                    pendingBytes += datagramBytes;
                    tail = RunAfter(tail, client, datagram, datagramBytes);
                    completion = tail;
                    return true;
                }
            }

            public void Retire()
            {
                lock (sync)
                    retired = true;
            }

            private async Task RunAfter(
                Task predecessor,
                AsyncTcpClient client,
                ServerDatagram datagram,
                int datagramBytes)
            {
                try
                {
                    await predecessor.ConfigureAwait(false);
                    lock (sync)
                    {
                        if (retired) return;
                    }
                    await handler(client, datagram).ConfigureAwait(false);
                }
                finally
                {
                    lock (sync)
                    {
                        pendingDatagrams--;
                        pendingBytes -= datagramBytes;
                    }
                }
            }
        }

    }
}