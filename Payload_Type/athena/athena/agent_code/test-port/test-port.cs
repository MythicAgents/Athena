using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "test-port";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
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
                        await messageManager.AddResponse(new TaskResponse
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
                    messageManager.WriteLine("No targets provided!", job.task.id, true, "error");
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
                    messageManager.WriteLine(sb.ToString(), job.task.id, false);
                });

                messageManager.WriteLine("", job.task.id, true);
            }
            catch (Exception e)
            {
                messageManager.WriteLine(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);

            return allData.Split(Environment.NewLine);
        }
    }
}

