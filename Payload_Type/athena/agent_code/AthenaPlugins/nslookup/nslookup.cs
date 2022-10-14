using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using Athena.Plugins;
using System.Linq;

namespace Plugins
{
    public class Nslookup : AthenaPlugin
    {
        public override string Name => "nslookup";
        public override void Execute(Dictionary<string, string> args)
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
                    PluginHandler.AddResponse(new ResponseResult
                    {
                        completed = "true",
                        user_output = "A file was provided but contained no data",
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
                PluginHandler.AddResponse(new ResponseResult
                {
                    completed = "true",
                    user_output = "No targets provided",
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

            PluginHandler.AddResponse(new ResponseResult
            {
                completed = "true",
                user_output = sb.ToString(),
                task_id = args["task-id"],
            });
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = System.Text.Encoding.ASCII.GetString(b);

            return allData.Split(Environment.NewLine);
        }
    }
}
