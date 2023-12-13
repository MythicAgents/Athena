using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Athena.Models;
using Athena.Commands.Models;
using Athena.Commands;
using Athena.Models.Responses;
using Athena.Models.Comms.Tasks;

namespace Plugins
{
    public class TestPort : IPlugin
    {
        public string Name => "test-port";

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
                    TaskResponseHandler.WriteLine("No targets provided!", args["task-id"], true, "error");
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
                    TaskResponseHandler.WriteLine(sb.ToString(), args["task-id"], false);
                });

                TaskResponseHandler.WriteLine("", args["task-id"], true);
            }
            catch (Exception e)
            {
                TaskResponseHandler.WriteLine(e.ToString(), args["task-id"], true, "error");
                return;
            }
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

