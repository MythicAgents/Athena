using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using Athena.Commands.Models;
using System.Linq;
using Athena.Models;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class Nslookup : IPlugin
    {
        public string Name => "nslookup";

        public bool Interactive => false;

        public void Interact(InteractiveMessage message)
        {
            throw new NotImplementedException();
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public void Start(Dictionary<string, string> args)
        {
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
                    TaskResponseHandler.AddResponse(new ResponseResult
                    {
                        completed = true,
                        process_response = new Dictionary<string, string> { { "message", "0x24" } },
                        task_id = args["task-id"],
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
                TaskResponseHandler.AddResponse(new ResponseResult
                {
                    completed = true,
                    process_response = new Dictionary<string, string> { { "message", "0x25" } },
                    task_id = args["task-id"],
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

            TaskResponseHandler.AddResponse(new ResponseResult
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = args["task-id"],
            });
        }

        public void Stop(string task_id)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = System.Text.Encoding.ASCII.GetString(b);

            return allData.Split(Environment.NewLine);
        }
    }
}
