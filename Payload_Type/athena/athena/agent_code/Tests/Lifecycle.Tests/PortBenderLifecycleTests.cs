extern alias portbender;

using System.Net;
using System.Net.Sockets;
using TcpForwarderSlim = portbender::port_bender.TcpForwarderSlim;

namespace Lifecycle.Tests;

[TestClass]
public class PortBenderLifecycleTests
{
    [TestMethod]
    public async Task PortBenderStartsAcceptsAConnectionAndStops()
    {
        var forwarder = new TcpForwarderSlim();
        await forwarder.StartAsync(
            new IPEndPoint(IPAddress.Loopback, 0),
            new IPEndPoint(IPAddress.Loopback, 9));

        IPEndPoint bound = forwarder.LocalEndpoint!;
        using (var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            await probe.ConnectAsync(bound);

        await forwarder.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));

        using var replacement = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        replacement.Bind(bound);
    }
}
