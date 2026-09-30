using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using arp;
using System.Net;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "arp";
        private readonly IMessageManager messageManager;
        private readonly ArpScanner scanner;

        public Plugin(
            IMessageManager messageManager,
            IAgentConfig config,
            ILogger logger,
            ITokenManager tokenManager,
            ISpawner spawner,
            IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            scanner = new ArpScanner(new NativeArpResolver());
        }

        public async Task Execute(ServerJob job)
        {
            var args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                var network = IPNetwork.Parse(args["cidr"]);
                var addresses = network.ListIPAddress().Select(address => address.ToString());
                var deadline = TimeSpan.FromSeconds(int.Parse(args["timeout"]));

                var results = await scanner.ScanAsync(
                    addresses,
                    deadline,
                    job.cancellationtokensource.Token).ConfigureAwait(false);
                foreach (var result in results)
                {
                    messageManager.WriteLine(result, job.task.id, false);
                }
                messageManager.Write("Finished Executing", job.task.id, true);
            }
            catch (Exception exception)
            {
                messageManager.Write(exception.ToString(), job.task.id, true, "error");
            }
        }
    }
}
