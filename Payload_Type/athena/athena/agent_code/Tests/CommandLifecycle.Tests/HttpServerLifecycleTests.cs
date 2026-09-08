extern alias httpserver;

using Agent.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HttpServerPlugin = httpserver::Agent.Plugin;

namespace CommandLifecycle.Tests;

[TestClass]
public sealed class HttpServerLifecycleTests
{
    [TestMethod]
    public async Task HttpServerServesHostedBytesOverLoopback()
    {
        var messages = new RecordingMessageManager();
        var plugin = new HttpServerPlugin(messages, null!, null!, null!, null!, null!);
        byte[] expected = { 0, 1, 2, 127, 128, 255 };
        int port = GetUnusedLoopbackPort();
        ServerJob startJob = Job("server", "start", port);
        messages.Jobs.Add(startJob.task.id, startJob);

        Task running = plugin.Execute(startJob);
        await WaitUntilAsync(() => messages.Responses.Any(response => response.Contains($"Started on port {port}")));
        await plugin.Execute(Job("host", "host", fileName: "payload.bin", fileContents: Convert.ToBase64String(expected)));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        using HttpResponseMessage response = await client.GetAsync($"http://localhost:{port}/payload.bin");
        byte[] actual = await response.Content.ReadAsByteArrayAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(expected, actual);
        Assert.IsTrue(messages.Responses.Any(item => item.Contains("Request for") && item.Contains("payload.bin")));

        await plugin.Execute(Job("stop", "stop"));
        await running.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsTrue(messages.Responses.Any(item => item.Contains("Server exit.")));
    }

    private static ServerJob Job(string id, string action, int port = 0, string fileName = "", string fileContents = "") => new(new ServerTask
    {
        id = id,
        command = "http-server",
        parameters = JsonSerializer.Serialize(new { action, port, fileName, fileContents })
    });

    private static int GetUnusedLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
}
