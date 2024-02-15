using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using nslookup;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "nslookup";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            NsLookupArgs args = JsonSerializer.Deserialize<NsLookupArgs>(job.task.parameters);
            StringBuilder sb = new StringBuilder();

            if (!args.Validate(out var message))
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", message } },
                    task_id = job.task.id,
                    status = "error",
                });
            }

            IEnumerable<string> hosts;

;


            if (!string.IsNullOrEmpty(args.targetlist)){
                hosts = GetTargetsFromFile(Misc.Base64DecodeToByteArray(args.targetlist));
            }
            else
            {
                hosts = args.hosts.Split(',');
            }

            foreach (var host in hosts)
            {
                try
                {
                    IPAddress ipAddy;

                    if (IPAddress.TryParse(host, out ipAddy))
                    {
                        sb.AppendLine(ReverseLookup(ipAddy));
                    }
                    else
                    {
                        sb.AppendLine(LookUpByHost(host));
                    }
                }
                catch (Exception e)
                {
                    sb.AppendLine(String.Format($"{host}\t\tNOTFOUND"));
                }
            }

            await messageManager.AddResponse(new TaskResponse
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = job.task.id,
            });
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);

            return allData.Split(Environment.NewLine);
        }

        private string ReverseLookup(IPAddress ip)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                IPHostEntry hostInfo = Dns.GetHostByAddress(ip);
                IPAddress[] address = hostInfo.AddressList;

                foreach(var alias in hostInfo.Aliases)
                {
                    sb.AppendLine(String.Format($"{ip}\t\t{alias}"));
                }
            }
            catch
            {
                sb.AppendLine(String.Format($"{ip}\t\tNOTFOUND"));
            }

            return sb.ToString();
        }

        private string LookUpByHost(string host)
        {
            StringBuilder sb = new StringBuilder(); 
            try
            {
                foreach (var ip in Dns.GetHostEntry(host).AddressList)
                {
                    sb.AppendLine(String.Format($"{host}\t\t{ip}"));
                }
            }
            catch
            {
                sb.AppendLine(String.Format($"{host}\t\tNOTFOUND"));
            }

            return sb.ToString();
        }
    }
}
