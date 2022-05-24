using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace Athena.Models.Athena.Socks
{
    public class AsyncTcpClient : IDisposable
    {
        private TcpClient tcpClient;
        private Stream stream;

        private int minBufferSize = 81920;
        private int maxBufferSize = 100 * 1024 * 1024;
        private int bufferSize = 81920;
        private bool disposed = false;

        private int BufferSize
        {
            get
            {
                return this.bufferSize;
            }
            set
            {
                this.bufferSize = value;
                if (this.tcpClient != null)
                    this.tcpClient.ReceiveBufferSize = value;
            }
        }

        public bool IsReceiving { set; get; }
        public int MinBufferSize
        {
            get
            {
                return this.minBufferSize;
            }
            set
            {
                this.minBufferSize = value;
            }
        }

        public int MaxBufferSize
        {
            get
            {
                return this.maxBufferSize;
            }
            set
            {
                this.maxBufferSize = value;
            }
        }

        public int SendBufferSize
        {
            get
            {
                if (this.tcpClient != null)
                    return this.tcpClient.SendBufferSize;
                else
                    return 0;
            }
            set
            {
                if (this.tcpClient != null)
                    this.tcpClient.SendBufferSize = value;
            }
        }

        public event EventHandler<byte[]> OnDataReceived;
        public event EventHandler OnDisconnected;

        public bool IsConnected
        {
            get
            {
                return this.tcpClient != null && this.tcpClient.Connected;
            }
        }

        public AsyncTcpClient()
        {

        }

        public async Task SendAsync(byte[] data, CancellationToken token = default(CancellationToken))
        {
            try
            {
                await this.stream.WriteAsync(data, 0, data.Length, token);
                await this.stream.FlushAsync(token);
            }
            catch (IOException ex)
            {
                var onDisconnected = this.OnDisconnected;
                if (ex.InnerException != null && ex.InnerException is ObjectDisposedException)
                {
                    // for SSL streams
                }
                else if (onDisconnected != null)
                {
                    onDisconnected(this, EventArgs.Empty);
                }
            }
            catch (Exception)
            {

            }
        }
        public async Task ConnectAsync(IPAddress host, int port, bool ssl = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                //Connect async method
                await this.CloseAsync();
                cancellationToken.ThrowIfCancellationRequested();
                this.tcpClient = new TcpClient();
                cancellationToken.ThrowIfCancellationRequested();
                await this.tcpClient.ConnectAsync(host, port);
                await this.CloseIfCanceled(cancellationToken);
                // get stream and do SSL handshake if applicable

                this.stream = this.tcpClient.GetStream();
                await this.CloseIfCanceled(cancellationToken);
                //if (ssl)
                //{
                //    var sslStream = new SslStream(this.stream);
                //    await sslStream.AuthenticateAsClientAsync(host);
                //    this.stream = sslStream;
                //    await this.CloseIfCanceled(cancellationToken);
                //}
            }
            catch (Exception)
            {
                this.CloseIfCanceled(cancellationToken).Wait();
                throw;
            }
        }

        public async Task Receive(CancellationToken token = default(CancellationToken))
        {
            try
            {
                if (!this.IsConnected || this.IsReceiving)
                    throw new InvalidOperationException();
                this.IsReceiving = true;
                byte[] buffer = new byte[bufferSize];
                while (this.IsConnected)
                {
                    token.ThrowIfCancellationRequested();
                    int bytesRead = await this.stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead > 0)
                    {
                        if (bytesRead == buffer.Length)
                            this.BufferSize = Math.Min(this.BufferSize * 10, this.maxBufferSize);
                        else
                        {
                            do
                            {
                                int reducedBufferSize = Math.Max(this.BufferSize / 10, this.minBufferSize);
                                if (bytesRead < reducedBufferSize)
                                    this.BufferSize = reducedBufferSize;

                            }
                            while (bytesRead > this.minBufferSize);
                        }
                        var onDataReceived = this.OnDataReceived;
                        if (onDataReceived != null)
                        {
                            byte[] data = new byte[bytesRead];
                            Array.Copy(buffer, data, bytesRead);
                            onDataReceived(this, data);
                        }
                    }
                    buffer = new byte[bufferSize];
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException ex)
            {
                var evt = this.OnDisconnected;
                if (ex.InnerException != null && ex.InnerException is ObjectDisposedException)
                {
                    // for SSL streams
                }
                if (evt != null)
                {
                    evt(this, EventArgs.Empty);
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                this.IsReceiving = false;
            }

        }


        public async Task CloseAsync()
        {
            await Task.Yield();
            this.Close();
        }

        private void Close()
        {
            if (this.tcpClient != null)
            {
                this.tcpClient.Dispose();
                this.tcpClient = null;
            }
            if (this.stream != null)
            {
                this.stream.Dispose();
                this.stream = null;
            }
        }

        private async Task CloseIfCanceled(CancellationToken token, Action onClosed = null)
        {
            if (token.IsCancellationRequested)
            {
                await this.CloseAsync();
                if (onClosed != null)
                    onClosed();
                token.ThrowIfCancellationRequested();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!disposed)
                {
                    this.Close();
                }
            }

            disposed = true;

            // If it is available, make the call to the
            // base class's Dispose(Boolean) method
            // base.Dispose(disposing);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}