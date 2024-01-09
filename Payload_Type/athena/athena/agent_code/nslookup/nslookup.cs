using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
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

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            NsLookupArgs args = JsonSerializer.Deserialize<NsLookupArgs>(job.task.parameters);
            StringBuilder sb = new StringBuilder();

            if (!args.Validate(out var message))
            {
                await messageManager.AddResponse(new ResponseResult
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", message } },
                    task_id = job.task.id,
                    status = "error",
                });
            }

            IEnumerable<string> hosts;


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
                    foreach (var ip in Dns.GetHostEntry(host).AddressList)
                    {
                        sb.Append(String.Format($"{host}\t\t{ip}") + Environment.NewLine);
                    }
                }
                catch (Exception e)
                {
                    sb.Append(String.Format($"{host}\t\tNOTFOUND") + Environment.NewLine);
                }
            }

            await messageManager.AddResponse(new ResponseResult
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = job.task.id,
            });
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = System.Text.Encoding.ASCII.GetString(b);

            return allData.Split(Environment.NewLine);
        }
    }
}
