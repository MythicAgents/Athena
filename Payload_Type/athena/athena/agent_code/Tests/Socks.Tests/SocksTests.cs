using Microsoft.VisualStudio.TestTools.UnitTesting;
using Agent.Interfaces;
using Agent.Models;
using System.Reflection;

namespace Socks.Tests;

[TestClass]
public class SocksTests
{
    [TestMethod]
    public async Task SocksConnectsForwardsDataAndCloses()
    {
        var messages = DispatchProxy.Create<IMessageManager, RecordingProxy>();
        var recorder = (RecordingProxy)(object)messages;
        var client = new FakeSocksClient(41)
        {
            LocalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("10.20.30.40"), 0x1234)
        };
        var plugin = new Agent.Plugin(
            messages, Stub<IAgentConfig>(), Stub<ILogger>(), Stub<ITokenManager>(),
            Stub<ISpawner>(), Stub<IPythonManager>(), _ => client);

        await plugin.HandleDatagram(ConnectRequest(41));
        await plugin.HandleDatagram(new ServerDatagram(41, new byte[] { 1, 2, 3 }, false));
        await plugin.HandleDatagram(new ServerDatagram(41, Array.Empty<byte>(), true));

        ServerDatagram reply = recorder.Datagrams.Single();
        CollectionAssert.AreEqual(
            new byte[] { 0x05, 0x00, 0x00, 0x01, 10, 20, 30, 40, 0x12, 0x34 },
            reply.bdata);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, client.Sent.Single());
        Assert.IsTrue(client.Disposed);
    }

    private static ServerDatagram ConnectRequest(int id) =>
        new(id, new byte[] { 0x05, 0x01, 0x00, 0x01, 127, 0, 0, 1, 0, 80 }, false);

    private static T Stub<T>() where T : class => DispatchProxy.Create<T, RecordingProxy>();
}

public class RecordingProxy : DispatchProxy
{
    public List<ServerDatagram> Datagrams { get; } = new();
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if ((targetMethod!.Name == nameof(IMessageManager.AddDatagram) ||
             targetMethod.Name == nameof(IMessageManager.TryAddDatagram)) &&
            args![1] is ServerDatagram datagram)
            Datagrams.Add(datagram);
        if (targetMethod.Name == nameof(IMessageManager.TryAddDatagram)) return true;
        if (targetMethod.ReturnType == typeof(void)) return null;
        if (targetMethod.ReturnType == typeof(Task)) return Task.CompletedTask;
        return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}

public sealed class FakeSocksClient(int id) : Agent.ISocksClient
{
    public int ServerId { get; } = id;
    public System.Net.IPEndPoint? LocalEndPoint { get; set; }
    public bool Disposed { get; private set; }
    public List<byte[]> Sent { get; } = new();
    public event Action<int>? Connected;
    public event Action<int>? Disconnected;
    public event Action<Nager.TcpClient.DataReceivedEventArgs>? DataReceived;
    public Task<bool> ConnectAsync(string host, int port, CancellationToken token = default)
    {
        Connected?.Invoke(ServerId);
        return Task.FromResult(true);
    }
    public Task SendAsync(byte[] data, CancellationToken token = default)
    {
        Sent.Add(data);
        return Task.CompletedTask;
    }
    public void Disconnect() => Disconnected?.Invoke(ServerId);
    public void Dispose() => Disposed = true;
}
