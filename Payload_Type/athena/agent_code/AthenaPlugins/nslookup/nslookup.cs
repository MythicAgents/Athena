using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using PluginBase;

namespace Athena
{
    public static class Plugin
    {
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
            if (args.ContainsKey("hosts"))
            {
                string[] hosts = args["hosts"].ToString().Split(',');
                foreach (var host in hosts)
                {
                    try
                    {
                        foreach(var ip in Dns.GetHostEntry(host).AddressList)
                        {
                            sb.Append(String.Format($"{host}\t\t{ip}") + Environment.NewLine);
                        }
                    }
                    catch (Exception e)
                    {
                        sb.Append(String.Format($"{host}\t\tNOTFOUND") + Environment.NewLine);
                    }
                }
                return new ResponseResult
                {
                    completed = "true",
                    user_output = sb.ToString(),
                    task_id = (string)args["task-id"],
                };
            }
            else
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = "No hosts specified.",
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }
    }
}
