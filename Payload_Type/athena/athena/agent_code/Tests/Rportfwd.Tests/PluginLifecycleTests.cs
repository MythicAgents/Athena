using Agent;
using Agent.Interfaces;
using Agent.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Rportfwd.Tests;

[TestClass]
public class RportfwdSmokeTests
{
    [TestMethod]
    public async Task RportfwdStartsExchangesDataAndStops()
    {
        int port = GetUnusedPort();
        var manager = DispatchProxy.Create<IMessageManager, RecordingMessageManager>();
        var recorder = (RecordingMessageManager)(object)manager;
        var plugin = new Agent.Plugin(manager, null!, null!, null!, null!, null!);

        await plugin.Execute(new ServerJob(new ServerTask
        {
            id = "rportfwd",
            command = "rportfwd",
            parameters = $"{{\"lport\":\"{port}\"}}"
        }));
        Assert.IsTrue(recorder.Responses.Any(response => response.user_output == "Listening."));

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await WaitUntilAsync(() => recorder.Datagrams.Any());
        int connectionId = recorder.Datagrams.First().server_id;

        byte[] inbound = { 1, 2, 3 };
        await client.GetStream().WriteAsync(inbound);
        await WaitUntilAsync(() => recorder.Datagrams.Any(datagram => datagram.server_id == connectionId && datagram.bdata.SequenceEqual(inbound)));

        byte[] outbound = { 4, 5, 6 };
        await plugin.HandleDatagram(new ServerDatagram(connectionId, outbound, false));
        byte[] received = new byte[outbound.Length];
        await client.GetStream().ReadExactlyAsync(received).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        CollectionAssert.AreEqual(outbound, received);

        Assert.IsTrue(await plugin.StopAsync(port));
    }

    private static int GetUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
}

public class RecordingMessageManager : DispatchProxy
{
    public ConcurrentQueue<TaskResponse> Responses { get; } = new();
    public ConcurrentQueue<ServerDatagram> Datagrams { get; } = new();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == nameof(IMessageManager.AddTaskResponse) && args![0] is TaskResponse response)
            Responses.Enqueue(response);
        if (targetMethod.Name == nameof(IMessageManager.TryAddDatagram))
        {
            if (args![1] is ServerDatagram datagram) Datagrams.Enqueue(datagram);
            return true;
        }
        if (targetMethod.ReturnType == typeof(void)) return null;
        if (targetMethod.ReturnType == typeof(Task)) return Task.CompletedTask;
        return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
