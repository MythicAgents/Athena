using System.Net;

namespace Agent;

public interface IHttpServerListener : IDisposable
{
    void Start(int port, bool ssl);
    Task<HttpListenerContext> GetContextAsync();
    void Stop();
}

internal sealed class SystemHttpServerListener : IHttpServerListener
{
    private readonly HttpListener listener = new();

    public void Start(int port, bool ssl)
    {
        listener.Prefixes.Add($"http://localhost:{port}/");
        if (ssl)
        {
            listener.Prefixes.Add($"https://localhost:{port}/");
        }
        listener.Start();
    }

    public Task<HttpListenerContext> GetContextAsync() => listener.GetContextAsync();
    public void Stop() => listener.Stop();
    public void Dispose() => listener.Close();
}
