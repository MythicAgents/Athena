using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using PluginBase;

namespace Plugin
{
    public static class testport
    {
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                string[] hosts = args["hosts"].ToString().Split(',');
                string[] ports = args["ports"].ToString().Split(',');
                
                StringBuilder output = new StringBuilder();
                Parallel.ForEach(hosts, host => //1 thread per host
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Host: " + host);
                    sb.AppendLine("---------------------------");
                    foreach(var port in ports)
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

                   output.AppendLine(sb.ToString() + Environment.NewLine);   
                });
                
                
                return new ResponseResult
                {
                    completed = "true",
                    user_output = output.ToString(),
                    task_id = (string)args["task-id"],
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = e.Message,
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }
    }

}

