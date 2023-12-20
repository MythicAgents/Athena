using System;
using System.Net;
using System.Collections.Generic;
using System.Text;

using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace nslookup
{
    public class Nslookup : IPlugin
    {
        public string Name => "nslookup";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Nslookup(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            StringBuilder sb = new StringBuilder();


            string[] hosts;

            if (args.ContainsKey("targetlist"))
            {
                if (args["targetlist"].ToString() != "")
                {
                    hosts = GetTargetsFromFile(Convert.FromBase64String(args["targetlist"].ToString())).ToArray<string>();
                }
                else
                {
                    await messageManager.AddResponse(new ResponseResult
                    {
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x24" } },
                        task_id = job.task.id,
                        status = "error",
                    });
                    return;
                }
            }
            else
            {
                hosts = args["hosts"].ToString().Split(',');
            }

            if (hosts.Count() < 1)
            {
                await messageManager.AddResponse(new ResponseResult
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x25" } },
                    task_id = job.task.id,
                    status = "error",
                });
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
