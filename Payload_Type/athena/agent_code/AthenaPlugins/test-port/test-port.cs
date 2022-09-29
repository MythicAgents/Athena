using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Athena.Plugins;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "test-port";
        public override void Execute(Dictionary<string, object> args)
        {
            try
            {
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
                            user_output = "A file was provided but contained no target data",
                            task_id = (string)args["task-id"],
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
                    PluginHandler.WriteLine("No targets provided!", (string)args["task-id"], true, "error");
                    return;
                }

                string[] ports = args["ports"].ToString().Split(',');

                Parallel.ForEach(hosts, host => //1 thread per host
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Host: " + host);
                    sb.AppendLine("---------------------------");
                    foreach (var port in ports)
                    {
                        try
                        {
                            using (TcpClient tcpClient = new TcpClient())
                            {
                                try
                                {
                                    tcpClient.ConnectAsync(host, int.Parse(port)).Wait(3000);

                                    if (tcpClient.Connected)
                                    {
                                        sb.AppendLine(port + " - Open");
                                    }
                                    else
                                    {
                                        sb.AppendLine(port + " - Closed");
                                    }
                                }
                                catch (Exception e)
                                {
                                    sb.AppendLine(port + " - " + e.GetType().ToString());
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            sb.AppendLine(e.ToString());
                        }
                    }
                    PluginHandler.WriteLine(sb.ToString(), (string)args["task-id"], false);
                });

                PluginHandler.WriteLine("", (string)args["task-id"], true);
            }
            catch (Exception e)
            {
                PluginHandler.WriteLine(e.ToString(), (string)args["task-id"], true, "error");
                return;
            }
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = System.Text.Encoding.ASCII.GetString(b);

            return allData.Split(Environment.NewLine);
        }
    }
}

