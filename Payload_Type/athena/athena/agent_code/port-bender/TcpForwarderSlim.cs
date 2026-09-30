using System.Net;
using System.Net.Sockets;

namespace port_bender
{
    public sealed class TcpForwarderSlim
    {
        private enum ForwarderState { Stopped, Running, Stopping }

        private readonly object stateLock = new();
        private readonly HashSet<Task> connections = new();
        private ForwarderState state;
        private Socket? listener;
        private CancellationTokenSource? cancellation;
        private Task? acceptLoop;

        public IPEndPoint? LocalEndpoint
        {
            get
            {
                lock (stateLock) return listener?.LocalEndPoint as IPEndPoint;
            }
        }

        public Task StartAsync(IPEndPoint local, IPEndPoint remote)
        {
            lock (stateLock)
            {
                if (state != ForwarderState.Stopped)
                    throw new InvalidOperationException("The forwarder is already running or stopping.");

                var newListener = new Socket(local.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    newListener.Bind(local);
                    newListener.Listen(10);
                }
                catch
                {
                    newListener.Dispose();
                    throw;
                }

                var newCancellation = new CancellationTokenSource();
                listener = newListener;
                cancellation = newCancellation;
                state = ForwarderState.Running;
                acceptLoop = AcceptLoopAsync(newListener, remote, newCancellation.Token);
                return Task.CompletedTask;
            }
        }

        public async Task StopAsync()
        {
            Task? loop;
            lock (stateLock)
            {
                if (state == ForwarderState.Stopped)
                    return;
                if (state == ForwarderState.Running)
                {
                    state = ForwarderState.Stopping;
                    cancellation!.Cancel();
                    listener!.Dispose();
                }
                loop = acceptLoop;
            }

            if (loop is not null)
                await loop.ConfigureAwait(false);

            lock (stateLock)
            {
                listener = null;
                acceptLoop = null;
                cancellation?.Dispose();
                cancellation = null;
                state = ForwarderState.Stopped;
            }
        }

        private async Task AcceptLoopAsync(Socket listeningSocket, IPEndPoint remote, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    Socket source = await listeningSocket.AcceptAsync(token).ConfigureAwait(false);
                    Task connection = ForwardConnectionAsync(source, remote, token);
                    lock (stateLock) connections.Add(connection);
                    _ = connection.ContinueWith(
                        completed => { lock (stateLock) connections.Remove(completed); },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                Task[] active;
                lock (stateLock) active = connections.ToArray();
                try
                {
                    await Task.WhenAll(active).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (SocketException) when (token.IsCancellationRequested)
                {
                }
            }
        }

        private static async Task ForwardConnectionAsync(Socket source, IPEndPoint remote, CancellationToken token)
        {
            using (source)
            using (var destination = new Socket(remote.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                await destination.ConnectAsync(remote, token).ConfigureAwait(false);
                using var sourceStream = new NetworkStream(source, ownsSocket: false);
                using var destinationStream = new NetworkStream(destination, ownsSocket: false);
                Task outbound = CopyAndHalfCloseAsync(sourceStream, destinationStream, destination, token);
                Task inbound = CopyAndHalfCloseAsync(destinationStream, sourceStream, source, token);
                await Task.WhenAll(outbound, inbound).ConfigureAwait(false);
            }
        }
        private static async Task CopyAndHalfCloseAsync(Stream input, Stream output, Socket outputSocket, CancellationToken token)
        {
            await input.CopyToAsync(output, token).ConfigureAwait(false);
            try { outputSocket.Shutdown(SocketShutdown.Send); }
            catch (SocketException) when (token.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
        }
    }
}
