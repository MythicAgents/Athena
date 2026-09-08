using Nager.TcpClient;

namespace Agent
{
    public interface ISocksClient : IDisposable
    {
        int ServerId { get; }
        System.Net.IPEndPoint? LocalEndPoint { get; }
        event Action<int>? Connected;
        event Action<int>? Disconnected;
        event Action<DataReceivedEventArgs>? DataReceived;
        Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
        Task SendAsync(byte[] data, CancellationToken cancellationToken = default);
        void Disconnect();
    }
}
