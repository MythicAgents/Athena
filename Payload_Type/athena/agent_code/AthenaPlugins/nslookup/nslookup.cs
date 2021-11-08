using System;
using System.Net;
using System.Collections.Generic;
using System.Text;

namespace Athena
{
    public static class Plugin
    {
        public static PluginResponse Execute(Dictionary<string, object> args)
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

                    return new PluginResponse()
                    {
                        success = true,
                        output = sb.ToString()
                    };
                }
                return new PluginResponse()
                {
                    success = true,
                    output = sb.ToString()
                };
            }
            else
            {
                return new PluginResponse()
                {
                    success = false,
                    output = "No hosts specified."
                };
            }
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
