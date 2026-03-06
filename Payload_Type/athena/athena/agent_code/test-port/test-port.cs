using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "test-port";
        private IDataBroker messageManager { get; set; }

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
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
                        messageManager.AddTaskResponse(new TaskResponse
                        {
                            completed = true,
                            user_output = "A file was provided but contained no data",
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

                DebugLog.Log($"{Name} scanning {hosts.Length} hosts, {ports.Length} ports [{job.task.id}]");
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
                DebugLog.Log($"{Name} completed [{job.task.id}]");
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
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

