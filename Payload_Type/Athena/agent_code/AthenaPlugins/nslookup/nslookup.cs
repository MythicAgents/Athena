using System;
using System.Net;
using System.Collections.Generic;
namespace Athena
{
    public static class Plugin
    {
        public static string Execute(Dictionary<string, object> args)
        {
            string output = "";
            if (args.ContainsKey("hosts"))
            {
                string[] hosts = args["hosts"].ToString().Split(',');
                foreach (var host in hosts)
                {
                    try
                    {
                        output += String.Format($"{host}\t\t{Dns.GetHostEntry(host)}") + Environment.NewLine;
                    }
                    catch (Exception e)
                    {
                        output += String.Format($"{host}\t\tNOTFOUND") + Environment.NewLine;
                    }
                }
                return output;
            }
            else
            {
                return "No hosts specified!";
            }
        }
    }
}
