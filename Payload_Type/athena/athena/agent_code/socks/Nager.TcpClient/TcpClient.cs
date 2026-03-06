using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nager.TcpClient
{
    /// <summary>
    /// A simple TcpClient
    /// </summary>
    public class TcpClient : IDisposable
    {
        private readonly TcpClientConfig _tcpClientConfig;

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationTokenRegistration _streamCancellationTokenRegistration;
        private readonly Task _dataReceiverTask;
        private readonly TcpClientKeepAliveConfig? _keepAliveConfig;
        private readonly object _connectSyncLock = new object();
        private readonly object _switchStateSyncLock = new object();
        private readonly SemaphoreSlim _connectionReady = new SemaphoreSlim(0, 1);

        private readonly byte[] _receiveBuffer;

        private System.Net.Sockets.TcpClient? _tcpClient;
        private bool _tcpClientInitialized;
        private Stream? _stream;
        private bool _isConnected;
        public int server_id = 0;
        /// <summary>
        /// Is client connected
        /// </summary>
        public bool IsConnected { get { return _isConnected; } }

        /// <summary>
        /// Event to call when the connection is established.
        /// </summary>
        public event Action<int>? Connected;

        /// <summary>
        /// Event to call when the connection is destroyed.
        /// </summary>
        public event Action<int>? Disconnected;

        /// <summary>
        /// Event to call when byte data has become available from the server.
        /// </summary>
        public event Action<DataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// TcpClient
        /// </summary>
        /// <param name="clientConfig"></param>
        /// <param name="keepAliveConfig"></param>
        /// <param name="logger"></param>
        public TcpClient(int server_id,
            TcpClientConfig? clientConfig = default,
            TcpClientKeepAliveConfig? keepAliveConfig = default)
        {
            this.server_id = server_id;
            this._cancellationTokenSource = new CancellationTokenSource();
            this._keepAliveConfig = keepAliveConfig;

            if (clientConfig == default)
            {
                clientConfig = new TcpClientConfig();
            }

            this._tcpClientConfig = clientConfig;
            this._receiveBuffer = new byte[clientConfig.ReceiveBufferSize];

            this._streamCancellationTokenRegistration = this._cancellationTokenSource.Token.Register(() =>
            {
                if (this._stream == null)
                {
                    return;
                }

                this._stream.Close();
            });

            this._dataReceiverTask = Task.Run(async () => await this.DataReceiverAsync(this._cancellationTokenSource.Token), this._cancellationTokenSource.Token);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }



        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

                if (this._cancellationTokenSource != null)
                {
                    if (!this._cancellationTokenSource.IsCancellationRequested)
                    {
                        this._cancellationTokenSource.Cancel();
                    }

                    this._cancellationTokenSource.Dispose();
                }

                if (this._dataReceiverTask != null)
                {
                    if (this._dataReceiverTask.Status == TaskStatus.Running)
                    {
                        this._dataReceiverTask.Wait(50);
                    }
                }

                this._streamCancellationTokenRegistration.Dispose();
                this._connectionReady.Dispose();

                this.DisposeTcpClientAndStream();

            }
        }

        private void DisposeTcpClientAndStream()
        {
            if (this._stream != null)
            {
                if (this._stream.CanWrite || this._stream.CanRead || this._stream.CanSeek)
                {
                    this._stream?.Close();
                }

                this._stream?.Dispose();
            }

            if (this._tcpClientInitialized)
            {
                if (this._tcpClient != null)
                {
                    if (this._tcpClient.Connected)
                    {
                        this._tcpClient.Close();
                    }

                    this._tcpClient.Dispose();
                }

                this._tcpClientInitialized = false;
            }
        }

        private void PrepareStream()
        {
            if (this._tcpClient == null)
            {
                return;
            }

            this._stream = this._tcpClient.GetStream();

            if (this._keepAliveConfig != null)
            {
                this._tcpClient.SetKeepAlive(this._keepAliveConfig.KeepAliveTime, this._keepAliveConfig.KeepAliveInterval, this._keepAliveConfig.KeepAliveRetryCount);
            }
        }

        private bool SwitchToConnected()
        {
            lock (this._switchStateSyncLock)
            {
                if (this._isConnected)
                {
                    return false;
                }

                this._isConnected = true;
                this.Connected?.Invoke(this.server_id);

                return true;
            }
        }

        private bool SwitchToDisconnected()
        {
            lock (this._switchStateSyncLock)
            {
                if (!this._isConnected)
                {
                    return false;
                }

                if (this._tcpClient != null && this._tcpClient.Connected)
                {
                    this._tcpClient.Close();
                }

                this._isConnected = false;
                this.Disconnected?.Invoke(this.server_id);

                return true;
            }
        }

        private System.Net.Sockets.TcpClient CreateTcpClient()
        {
            return new System.Net.Sockets.TcpClient
            {
                ReceiveBufferSize = this._tcpClientConfig.ReceiveBufferSize,
                ReceiveTimeout = this._tcpClientConfig.ReceiveTimeout,
                SendBufferSize = this._tcpClientConfig.SendBufferSize,
                SendTimeout = this._tcpClientConfig.SendTimeout,
                NoDelay = this._tcpClientConfig.NoDelay
            };
        }

        /// <summary>
        /// Connect
        /// </summary>
        /// <param name="ipAddressOrHostname"></param>
        /// <param name="port"></param>
        /// <param name="connectTimeoutInMilliseconds">default: 2s</param>
        public bool Connect(
            string ipAddressOrHostname,
            int port,
            int connectTimeoutInMilliseconds = 2000)
        {
            ipAddressOrHostname = ipAddressOrHostname ?? throw new ArgumentNullException(nameof(ipAddressOrHostname));

            if (this._isConnected)
            {
                return false;
            }

            if (this._tcpClientInitialized)
            {
                return false;
            }

            lock (this._connectSyncLock)
            {
                if (this._tcpClientInitialized)
                {
                    return false;
                }

                this._tcpClientInitialized = true;

                try
                {
                    this._tcpClient = this.CreateTcpClient();

                    IAsyncResult asyncResult = this._tcpClient.BeginConnect(ipAddressOrHostname, port, null, null);
                    var waitHandle = asyncResult.AsyncWaitHandle;

                    //Try connect with timeout
                    if (!asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(connectTimeoutInMilliseconds), exitContext: false))
                    {

                        /*
                         * INFO
                         * Do not include a dispose for the waitHandle here this will cause an exception
                        */

                        this._tcpClient.Close();

                        this._tcpClientInitialized = false;
                        this._tcpClient.Dispose();

                        return false;
                    }

                    this._tcpClient.EndConnect(asyncResult);

                    waitHandle.Close();
                    waitHandle.Dispose();

                    this.PrepareStream();
                    this.SwitchToConnected();
                    this._connectionReady.Release();

                    return true;
                }
                catch (Exception exception)
                {
                    this._tcpClientInitialized = false;
                    this._tcpClient?.Dispose();
                }
            }

            return false;
        }


        /// <summary>
        /// ConnectAsync
        /// </summary>
        /// <param name="ipAddressOrHostname"></param>
        /// <param name="port"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> ConnectAsync(
            string ipAddressOrHostname,
            int port,
            CancellationToken cancellationToken = default)
        {
            ipAddressOrHostname = ipAddressOrHostname ?? throw new ArgumentNullException(nameof(ipAddressOrHostname));

            if (this._isConnected)
            {
                return false;
            }

            if (this._tcpClientInitialized)
            {
                return false;
            }

            this._tcpClientInitialized = true;
            this._tcpClient = this.CreateTcpClient();

            try
            {
                await this._tcpClient.ConnectAsync(ipAddressOrHostname, port, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                this._tcpClientInitialized = false;
                this._tcpClient?.Dispose();
                return false;
            }

            this.PrepareStream();
            this.SwitchToConnected();
            this._connectionReady.Release();

            return true;
        }

        /// <summary>
        /// Disconnect
        /// </summary>
        public void Disconnect()
        {
            this.DisposeTcpClientAndStream();
            this.SwitchToDisconnected();
        }

        /// <summary>
        /// Send data async
        /// </summary>
        /// <param name="data"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SendAsync(
            byte[] data,
            CancellationToken cancellationToken = default)
        {
            if (this._stream == null)
            {
                return;
            }

            await this._stream.WriteAsync(data.AsMemory(0, data.Length), cancellationToken).ConfigureAwait(false);
            await this._stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        }

        private async Task DataReceiverAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await this._connectionReady.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                if (this._stream == null || this._tcpClient == null || !this._tcpClient.Connected)
                {
                    this.SwitchToDisconnected();
                    break;
                }

                try
                {
                    byte[] data = await DataReadAsync(cancellationToken).ConfigureAwait(false);

                    if (data.Length == 0)
                    {
                        this.SwitchToDisconnected();
                        break;
                    }

                    this.DataReceived?.Invoke(new DataReceivedEventArgs
                    {
                        bytes = data,
                        server_id = server_id,
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    this.SwitchToDisconnected();
                    if (!this.IsKnownException(ex))
                    {
                        break;
                    }
                }
            }
        }

        private bool IsKnownException(Exception? exception)
        {
            if (exception == null)
            {
                return false;
            }

            IOException? ioException = exception as IOException
                ?? (exception as AggregateException)?.InnerException as IOException;

            if (ioException?.InnerException is SocketException socketException)
            {
                return socketException.SocketErrorCode is
                    SocketError.TimedOut or
                    SocketError.ConnectionReset or
                    SocketError.OperationAborted;
            }

            return false;
        }

        private async Task<byte[]> DataReadAsync(CancellationToken cancellationToken)
        {
            if (this._stream == null || !this._stream.CanRead)
            {
                return [];
            }

            var numberOfBytesRead = await this._stream.ReadAsync(this._receiveBuffer.AsMemory(0, this._receiveBuffer.Length), cancellationToken).ConfigureAwait(false);
            if (numberOfBytesRead == 0)
            {
                return [];
            }

            return this._receiveBuffer.AsSpan(0, numberOfBytesRead).ToArray();
        }
    }
}