using Agent.Interfaces;
using Agent.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class AsyncTCPClient2 : IDisposable
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private List<byte> _accumulatedBytes = new List<byte>();
    public bool IsConnected => _tcpClient?.Connected ?? false;

    // Events
    public event Action? Connected;
    public event Action<byte[]>? MessageReceived;
    public event Action? Disconnected;
    IMessageManager _mm;
    int _server_id = 0;

    public AsyncTCPClient2(IPAddress ipAddress, int port, IMessageManager mm, int server_id)
    {
        _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        _port = port;
        _mm = mm;
        _server_id = server_id;
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            throw new InvalidOperationException("Already connected.");

        _tcpClient = new TcpClient();

        try
        {
            await _tcpClient.ConnectAsync(_ipAddress, _port, cancellationToken).ConfigureAwait(false);
            _networkStream = _tcpClient.GetStream();
            // Start listening for messages
            _ = Task.Run(() => ListenForMessages(cancellationToken), cancellationToken);
            return IsConnected;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public async Task ForwardDataAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _networkStream == null)
            throw new InvalidOperationException("Not connected to the server.");

        if (data == null || data.Length == 0)
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _networkStream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
            await _networkStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Dispose();
            throw new IOException("Failed to send data to the server.", ex);
        }
        finally
        {
            _sendLock.Release();
        }
    }
    private async Task ListenForMessages(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var accumulatedBuffer = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                int bytesRead = await _networkStream!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0) break;
                
                await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    _accumulatedBytes.AddRange(buffer.Take(bytesRead));
                }
                catch{ }
                finally { _readLock.Release(); }
                

                // Process messages based on delimiter (e.g., newline `\n`)
                //while (true)
                //{
                //    int delimiterIndex = accumulatedBuffer.IndexOf((byte)'\n');
                //    if (delimiterIndex == -1) break;

                //    var messageBytes = accumulatedBuffer.Take(delimiterIndex).ToArray();
                //    accumulatedBuffer.RemoveRange(0, delimiterIndex + 1);

                    
                //    await _mm.AddResponse(Agent.Models.DatagramSource.Socks5, new Agent.Models.ServerDatagram(_server_id, messageBytes, false));
                //}
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // Handle exceptions gracefully
        }
        finally
        {
            Dispose();
        }
    }

    public async Task<ServerDatagram>? GetServerDatagram(CancellationToken cancellationToken)
    {
        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if(_accumulatedBytes.Count == 0)
            {
                return null;
            }
            byte[] buf = _accumulatedBytes.ToArray();
            _accumulatedBytes.Clear();
            Console.WriteLine($"Returning {buf.Length} bytes");
            ServerDatagram dg = new ServerDatagram(_server_id,buf,!IsConnected);
            return dg;
        }
        catch (Exception e){
            Console.WriteLine(e);
            return null;
        }
        finally { _readLock.Release(); }
    }
    private async Task ListenForMessagesOld(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                List<byte> msg = new List<byte>();
                int bytesRead = await _networkStream!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0) // Connection closed
                {
                    break;
                }

                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);
                await _mm.AddResponse(Agent.Models.DatagramSource.Socks5, new Agent.Models.ServerDatagram(_server_id, data, false));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // Swallow exceptions to ensure graceful disconnection
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (IsConnected)
        {
            Disconnected?.Invoke();
        }

        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _networkStream?.Dispose();
        _tcpClient = null;
        _networkStream = null;
    }
}