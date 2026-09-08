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
        private readonly IMessageManager messageManager;
        private readonly ConcurrentDictionary<int, ISocksClient> connections = new();
        private readonly ConcurrentDictionary<int, ConnectionLock> connectionLocks = new();
        private readonly ConcurrentDictionary<int, long> connectionGenerations = new();
        private long nextConnectionGeneration;
        private const int DefaultMaximumConnections = 128;
        // A blocked CONNECT may receive payload before it completes. Bound that
        // per-connection queue so an unreachable peer cannot retain unbounded memory.
        private const int DefaultMaximumPendingDatagrams = 64;
        private const int DefaultMaximumPendingBytes = 1024 * 1024;
        private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(10);
        private readonly Func<int, ISocksClient> clientFactory;
        private readonly Func<TimeSpan, CancellationTokenSource> connectTimeoutFactory;
        private readonly Func<TimeSpan, CancellationTokenSource> sendTimeoutFactory;
        private readonly SemaphoreSlim connectionSlots;
        private readonly int maximumPendingDatagrams;
        private readonly int maximumPendingBytes;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, DefaultClientFactory)
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, Func<int, ISocksClient> clientFactory)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, clientFactory, timeout => new CancellationTokenSource(timeout))
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, Func<int, ISocksClient> clientFactory, Func<TimeSpan, CancellationTokenSource> connectTimeoutFactory)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, clientFactory, connectTimeoutFactory, timeout => new CancellationTokenSource(timeout))
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, Func<int, ISocksClient> clientFactory, int maximumConnections)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, clientFactory, timeout => new CancellationTokenSource(timeout), timeout => new CancellationTokenSource(timeout), maximumConnections)
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, Func<int, ISocksClient> clientFactory, Func<TimeSpan, CancellationTokenSource> connectTimeoutFactory, Func<TimeSpan, CancellationTokenSource> sendTimeoutFactory)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, clientFactory, connectTimeoutFactory, sendTimeoutFactory, DefaultMaximumConnections)
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, Func<int, ISocksClient> clientFactory, Func<TimeSpan, CancellationTokenSource> connectTimeoutFactory, Func<TimeSpan, CancellationTokenSource> sendTimeoutFactory, int maximumConnections)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, clientFactory,
                connectTimeoutFactory, sendTimeoutFactory, maximumConnections,
                DefaultMaximumPendingDatagrams, DefaultMaximumPendingBytes)
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, Func<int, ISocksClient> clientFactory, int maximumConnections, int maximumPendingDatagrams, int maximumPendingBytes)
            : this(messageManager, config, logger, tokenManager, spawner, pythonManager, clientFactory,
                timeout => new CancellationTokenSource(timeout), timeout => new CancellationTokenSource(timeout),
                maximumConnections, maximumPendingDatagrams, maximumPendingBytes)
        {
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager, Func<int, ISocksClient> clientFactory, Func<TimeSpan, CancellationTokenSource> connectTimeoutFactory, Func<TimeSpan, CancellationTokenSource> sendTimeoutFactory, int maximumConnections, int maximumPendingDatagrams, int maximumPendingBytes)
        {
            if (maximumConnections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConnections));
            if (maximumPendingDatagrams <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPendingDatagrams));
            if (maximumPendingBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPendingBytes));
            this.messageManager = messageManager;
            this.clientFactory = clientFactory;
            this.connectTimeoutFactory = connectTimeoutFactory;
            this.sendTimeoutFactory = sendTimeoutFactory;
            connectionSlots = new SemaphoreSlim(maximumConnections, maximumConnections);
            this.maximumPendingDatagrams = maximumPendingDatagrams;
            this.maximumPendingBytes = maximumPendingBytes;
        }

        private static ISocksClient DefaultClientFactory(int serverId) => new Nager.TcpClient.TcpClient(
            serverId,
            new TcpClientConfig { NoDelay = true },
            new TcpClientKeepAliveConfig { KeepAliveTime = 60, KeepAliveInterval = 10, KeepAliveRetryCount = 5 });

        public Task Execute(ServerJob job) => Task.CompletedTask;

        public async Task HandleDatagram(ServerDatagram datagram)
        {
            if (!TryDecode(datagram, out byte[] bytes))
            {
                await RejectMalformedDatagram(datagram.server_id).ConfigureAwait(false);
                return;
            }
            datagram.bdata = bytes;

            ConnectionLock connectionLock = AcquireConnectionLock(datagram.server_id);
            PendingDatagram? pending = null;
            ConnectionAttempt? attempt = null;
            try
            {
                await connectionLock.Semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (connectionLock.IsConnecting)
                    {
                        // Local exit preempts the CONNECT and drops queued payload silently.
                        // Overflow rejects the entire CONNECT with one failure+exit; no queued
                        // prefix is forwarded. Both paths cancel CONNECT and complete all waiters.
                        if (datagram.exit)
                            AbortConnectingConnection(datagram.server_id, connectionLock, returnFailure: false);
                        else if (connectionLock.IsDuplicateConnect(datagram))
                            return;
                        else if (!connectionLock.TryEnqueue(datagram, out pending))
                            AbortConnectingConnection(datagram.server_id, connectionLock, returnFailure: true);
                    }
                    else
                    {
                        attempt = await HandleDatagramSerialized(datagram, connectionLock).ConfigureAwait(false);
                    }
                }
                finally
                {
                    connectionLock.Semaphore.Release();
                }

                if (attempt != null)
                    await CompleteConnectionAttempt(attempt, connectionLock).ConfigureAwait(false);
                else if (pending != null)
                    await pending.Completion.Task.ConfigureAwait(false);
            }
            finally
            {
                ReleaseConnectionLock(datagram.server_id, connectionLock);
            }
        }

        private bool TryDecode(ServerDatagram datagram, out byte[] bytes)
        {
            try
            {
                bytes = Base64Transfer.Decode(datagram.data, maximumPendingBytes);
                return true;
            }
            catch
            {
                bytes = Array.Empty<byte>();
                return false;
            }
        }

        private async Task RejectMalformedDatagram(int serverId)
        {
            ConnectionLock connectionLock = AcquireConnectionLock(serverId);
            try
            {
                await connectionLock.Semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (connectionLock.IsConnecting)
                    {
                        AbortConnectingConnection(serverId, connectionLock, returnFailure: true);
                        return;
                    }

                    if (connections.TryGetValue(serverId, out ISocksClient? client) &&
                        connectionGenerations.TryGetValue(serverId, out long generation) &&
                        RemoveAndDispose(serverId, client, generation))
                    {
                        ReturnExit(serverId);
                        return;
                    }

                    ReturnMessageFailure(serverId);
                }
                finally
                {
                    connectionLock.Semaphore.Release();
                }
            }
            finally
            {
                ReleaseConnectionLock(serverId, connectionLock);
            }
        }

        private ConnectionLock AcquireConnectionLock(int serverId)
        {
            while (true)
            {
                ConnectionLock connectionLock = connectionLocks.GetOrAdd(
                    serverId,
                    _ => new ConnectionLock(maximumPendingDatagrams, maximumPendingBytes));
                if (connectionLock.TryAddReference())
                    return connectionLock;
            }
        }

        private void ReleaseConnectionLock(int serverId, ConnectionLock connectionLock)
        {
            if (!connectionLock.ReleaseReference()) return;

            ((ICollection<KeyValuePair<int, ConnectionLock>>)connectionLocks)
                .Remove(new KeyValuePair<int, ConnectionLock>(serverId, connectionLock));
            connectionLock.Dispose();
        }

        private async Task<ConnectionAttempt?> HandleDatagramSerialized(ServerDatagram datagram, ConnectionLock connectionLock)
        {
            if (!connections.TryGetValue(datagram.server_id, out ISocksClient? client))
            {
                if (datagram.exit) return null;
                ConnectionAttempt? attempt = BeginConnectionAttempt(datagram, connectionLock);
                if (attempt == null) ReturnMessageFailure(datagram.server_id);
                return attempt;
            }

            if (connectionGenerations.TryGetValue(datagram.server_id, out long generation))
                await SendOrExit(datagram, client, generation).ConfigureAwait(false);
            return null;
        }

        private async Task SendOrExit(ServerDatagram datagram, ISocksClient client, long generation)
        {
            if (!string.IsNullOrEmpty(datagram.data))
            {
                try
                {
                    using var timeout = sendTimeoutFactory(DefaultSendTimeout);
                    await client.SendAsync(datagram.bdata, timeout.Token).ConfigureAwait(false);
                }
                catch
                {
                    if (RemoveAndDispose(datagram.server_id, client, generation))
                    {
                        ReturnExit(datagram.server_id);
                    }
                    return;
                }
            }

            if (datagram.exit && RemoveAndDispose(datagram.server_id, client, generation))
            {
                client.Disconnect();
            }
        }

        private ConnectionAttempt? BeginConnectionAttempt(ServerDatagram datagram, ConnectionLock connectionLock)
        {
            if (string.IsNullOrEmpty(datagram.data)) return null;
            var options = new ConnectionOptions(datagram.server_id, datagram.bdata);
            if (!options.Parse()) return null;
            if (!connectionSlots.Wait(0)) return null;

            CancellationTokenSource connectCancellation;
            try
            {
                connectCancellation = connectTimeoutFactory(DefaultConnectTimeout);
            }
            catch
            {
                connectionSlots.Release();
                return null;
            }

            long generation = Interlocked.Increment(ref nextConnectionGeneration);
            ISocksClient client;
            try
            {
                client = clientFactory(datagram.server_id);
            }
            catch
            {
                connectCancellation.Dispose();
                connectionSlots.Release();
                return null;
            }
            client.DataReceived += args => OnDataReceived(args, client, generation);
            client.Connected += id => OnConnected(id, client, generation);
            client.Disconnected += id => OnDisconnected(id, client, generation);

            if (!connections.TryAdd(datagram.server_id, client))
            {
                try
                {
                    client.Dispose();
                }
                finally
                {
                    connectCancellation.Dispose();
                    connectionSlots.Release();
                }
                return null;
            }
            connectionGenerations[datagram.server_id] = generation;
            connectionLock.BeginConnect(datagram, connectCancellation, generation);
            return new ConnectionAttempt(client, generation, options.host, options.port, datagram.server_id, connectCancellation);
        }

        private async Task CompleteConnectionAttempt(ConnectionAttempt attempt, ConnectionLock connectionLock)
        {
            using CancellationTokenSource timeout = attempt.ConnectCancellation;
            bool connected;
            try
            {
                connected = await attempt.Client.ConnectAsync(attempt.Host, attempt.Port, timeout.Token).ConfigureAwait(false);
            }
            catch
            {
                connected = false;
            }

            await connectionLock.Semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                bool current = IsCurrentConnection(attempt.ServerId, attempt.Client, attempt.Generation);
                if (!connected || !current)
                {
                    PendingDatagram[] failed = connectionLock.CompleteConnect(attempt.Generation);
                    try
                    {
                        if (current)
                            RemoveAndDispose(attempt.ServerId, attempt.Client, attempt.Generation);
                    }
                    finally
                    {
                        foreach (PendingDatagram queued in failed)
                            queued.Completion.TrySetResult();
                    }
                    if (current) ReturnMessageFailure(attempt.ServerId);
                    return;
                }

                PendingDatagram[] connectedQueue = connectionLock.CompleteConnect(attempt.Generation);
                for (int index = 0; index < connectedQueue.Length; index++)
                {
                    PendingDatagram queued = connectedQueue[index];
                    try
                    {
                        if (connections.TryGetValue(attempt.ServerId, out ISocksClient? client))
                            await SendOrExit(queued.Datagram, client, attempt.Generation).ConfigureAwait(false);
                    }
                    finally
                    {
                        queued.Completion.TrySetResult();
                        if (!IsCurrentConnection(attempt.ServerId, attempt.Client, attempt.Generation))
                            for (int remaining = index + 1; remaining < connectedQueue.Length; remaining++)
                                connectedQueue[remaining].Completion.TrySetResult();
                    }
                }
            }
            finally
            {
                connectionLock.Semaphore.Release();
            }
        }

        private void AbortConnectingConnection(int serverId, ConnectionLock connectionLock, bool returnFailure)
        {
            if (!connectionLock.TryGetConnectingGeneration(out long generation)) return;
            PendingDatagram[] queued = connectionLock.AbortConnect(generation);
            bool removed = connections.TryGetValue(serverId, out ISocksClient? client) &&
                IsCurrentConnection(serverId, client, generation);
            try
            {
                if (removed)
                    RemoveAndDispose(serverId, client!, generation);
            }
            finally
            {
                foreach (PendingDatagram pending in queued)
                    pending.Completion.TrySetResult();
            }
            if (removed && returnFailure) ReturnMessageFailure(serverId);
        }

        private bool RemoveAndDispose(int serverId, ISocksClient client, long generation)
        {
            bool removed = ((ICollection<KeyValuePair<int, ISocksClient>>)connections)
                .Remove(new KeyValuePair<int, ISocksClient>(serverId, client));
            if (!removed) return false;

            bool generationRemoved = false;
            try
            {
                client.Dispose();
            }
            finally
            {
                generationRemoved = ((ICollection<KeyValuePair<int, long>>)connectionGenerations)
                    .Remove(new KeyValuePair<int, long>(serverId, generation));
                connectionSlots.Release();
            }
            return generationRemoved;
        }

        public bool ReturnMessageFailure(int id) => messageManager.TryAddDatagram(
            DatagramSource.Socks5,
            new ServerDatagram(id, new ConnectResponse
            {
                bndaddr = new byte[] { 0x00, 0x00, 0x00, 0x00 },
                bndport = new byte[] { 0x00, 0x00 },
                addrtype = (byte)AddressType.IPv4,
                status = ConnectResponseStatus.GeneralFailure,
            }.ToByte(), true));

        public void ReturnSuccess(int id)
        {
            if (connections.TryGetValue(id, out ISocksClient? client) &&
                connectionGenerations.TryGetValue(id, out long generation))
                ReturnSuccess(id, null, client, generation);
        }

        private void ReturnSuccess(int id, System.Net.IPEndPoint? boundEndpoint, ISocksClient client, long generation)
        {
            byte[] address = boundEndpoint?.Address.GetAddressBytes() ?? new byte[4];
            byte addressType = address.Length == 16
                ? (byte)AddressType.IPv6
                : (byte)AddressType.IPv4;
            ushort port = (ushort)(boundEndpoint?.Port ?? 0);

            if (!messageManager.TryAddDatagram(
                DatagramSource.Socks5,
                new ServerDatagram(id, new ConnectResponse
                {
                    bndaddr = address,
                    bndport = new byte[] { (byte)(port >> 8), (byte)port },
                    addrtype = addressType,
                    status = ConnectResponseStatus.Success,
                }.ToByte(), false)) &&
                IsCurrentConnection(id, client, generation))
                RemoveAndDispose(id, client, generation);
        }

        private bool IsCurrentConnection(int serverId, ISocksClient client, long generation) =>
            connections.TryGetValue(serverId, out ISocksClient? current) &&
            ReferenceEquals(current, client) &&
            connectionGenerations.TryGetValue(serverId, out long currentGeneration) &&
            currentGeneration == generation;

        private void OnConnected(int serverId, ISocksClient client, long generation)
        {
            if (IsCurrentConnection(serverId, client, generation))
                ReturnSuccess(serverId, client.LocalEndPoint, client, generation);
        }

        private void OnDataReceived(DataReceivedEventArgs args, ISocksClient client, long generation)
        {
            if (IsCurrentConnection(client.ServerId, client, generation) &&
                !messageManager.TryAddDatagram(DatagramSource.Socks5, new ServerDatagram(client.ServerId, args.bytes, false)) &&
                IsCurrentConnection(client.ServerId, client, generation))
                RemoveAndDispose(client.ServerId, client, generation);
        }

        private void OnDisconnected(int serverId, ISocksClient client, long generation)
        {
            if (RemoveAndDispose(serverId, client, generation))
                ReturnExit(serverId);
        }

        private bool ReturnExit(int serverId) =>
            messageManager.TryAddDatagram(DatagramSource.Socks5, new ServerDatagram(serverId, Array.Empty<byte>(), true));


        private sealed class ConnectionLock : IDisposable
        {
            private readonly object sync = new();
            private readonly Queue<PendingDatagram> pending = new();
            private readonly int maximumPendingDatagrams;
            private readonly int maximumPendingBytes;
            private int pendingBytes;
            private int references;
            private bool retired;
            private byte[]? connectRequest;
            private CancellationTokenSource? connectCancellation;
            private long? connectGeneration;

            public ConnectionLock(int maximumPendingDatagrams, int maximumPendingBytes)
            {
                this.maximumPendingDatagrams = maximumPendingDatagrams;
                this.maximumPendingBytes = maximumPendingBytes;
            }

            public SemaphoreSlim Semaphore { get; } = new(1, 1);
            public bool IsConnecting => connectRequest != null;
            public bool TryGetConnectingGeneration(out long generation)
            {
                generation = connectGeneration.GetValueOrDefault();
                return connectGeneration.HasValue;
            }

            public bool TryAddReference()
            {
                lock (sync)
                {
                    if (retired) return false;
                    references++;
                    return true;
                }
            }

            public void BeginConnect(ServerDatagram datagram, CancellationTokenSource cancellation, long generation)
            {
                connectRequest = datagram.bdata.ToArray();
                connectCancellation = cancellation;
                connectGeneration = generation;
            }

            public bool IsDuplicateConnect(ServerDatagram datagram) =>
                !datagram.exit && connectRequest != null && datagram.bdata.SequenceEqual(connectRequest);

            public bool TryEnqueue(ServerDatagram datagram, out PendingDatagram? queued)
            {
                int datagramBytes = datagram.bdata.Length;
                if (pending.Count >= maximumPendingDatagrams ||
                    datagramBytes > maximumPendingBytes - pendingBytes)
                {
                    queued = null;
                    return false;
                }

                queued = new PendingDatagram(datagram);
                pending.Enqueue(queued);
                pendingBytes += datagramBytes;
                return true;
            }

            private PendingDatagram[] TakePending()
            {
                PendingDatagram[] queued = pending.ToArray();
                pending.Clear();
                pendingBytes = 0;
                return queued;
            }

            public PendingDatagram[] CompleteConnect(long generation)
            {
                if (connectGeneration != generation) return Array.Empty<PendingDatagram>();
                connectRequest = null;
                connectCancellation = null;
                connectGeneration = null;
                return TakePending();
            }

            public PendingDatagram[] AbortConnect(long generation)
            {
                if (connectGeneration != generation) return Array.Empty<PendingDatagram>();
                connectCancellation?.Cancel();
                return CompleteConnect(generation);
            }

            public bool ReleaseReference()
            {
                lock (sync)
                {
                    references--;
                    if (references != 0) return false;
                    retired = true;
                    return true;
                }
            }

            public void Dispose() => Semaphore.Dispose();
        }

        private sealed record ConnectionAttempt(ISocksClient Client, long Generation, string Host, int Port, int ServerId, CancellationTokenSource ConnectCancellation);

        private sealed record PendingDatagram(ServerDatagram Datagram)
        {
            public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
