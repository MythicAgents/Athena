using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "net-enum";
        private IDataBroker messageManager { get; set; }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

        private static uint macAddrLen = (uint)new byte[6].Length;

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<net_enum.NetEnumArgs>(job.task.parameters);

            if (args is null)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "Failed to deserialize arguments.",
                    task_id = job.task.id,
                    status = "error"
                });
                return;
            }

            try
            {
                switch (args.action)
                {
                    case "ping":
                        await ExecutePing(args, job.task.id);
                        break;
                    case "traceroute":
                        await ExecuteTraceroute(args, job.task.id);
                        break;
                    case "ifconfig":
                        ExecuteIfconfig(job.task.id);
                        break;
                    case "netstat":
                        ExecuteNetstat(job.task.id);
                        break;
                    case "arp":
                        await ExecuteArp(args, job.task.id);
                        break;
                    case "test-port":
                        ExecuteTestPort(args, job.task.id);
                        break;
                    default:
                        messageManager.AddTaskResponse(new TaskResponse
                        {
                            completed = true,
                            user_output = $"Unknown action: {args.action}",
                            task_id = job.task.id,
                            status = "error"
                        });
                        break;
                }
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }

            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }

        private async Task ExecutePing(net_enum.NetEnumArgs args, string task_id)
        {
            using var pinger = new Ping();
            var sb = new StringBuilder();
            int successes = 0;

            for (int i = 0; i < args.count; i++)
            {
                try
                {
                    var reply = await pinger.SendPingAsync(args.host, args.timeout);
                    if (reply.Status == IPStatus.Success)
                    {
                        sb.AppendLine($"Reply from {reply.Address}: time={reply.RoundtripTime}ms TTL={reply.Options?.Ttl ?? 0}");
                        successes++;
                    }
                    else
                    {
                        sb.AppendLine($"Request to {args.host}: {reply.Status}");
                    }
                }
                catch (PingException ex)
                {
                    sb.AppendLine($"Ping failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            sb.AppendLine($"\n{successes}/{args.count} packets received");
            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = task_id,
            });
        }

        private async Task ExecuteTraceroute(net_enum.NetEnumArgs args, string task_id)
        {
            using var pinger = new Ping();
            var sb = new StringBuilder();
            sb.AppendLine($"Traceroute to {args.host} (max {args.max_ttl} hops):");

            for (int ttl = 1; ttl <= args.max_ttl; ttl++)
            {
                try
                {
                    var options = new PingOptions(ttl, true);
                    var reply = await pinger.SendPingAsync(args.host, args.timeout, new byte[32], options);

                    if (reply.Status == IPStatus.TtlExpired)
                    {
                        sb.AppendLine($"  {ttl}\t{reply.Address}\t{reply.RoundtripTime}ms");
                    }
                    else if (reply.Status == IPStatus.Success)
                    {
                        sb.AppendLine($"  {ttl}\t{reply.Address}\t{reply.RoundtripTime}ms");
                        break;
                    }
                    else
                    {
                        sb.AppendLine($"  {ttl}\t*\t{reply.Status}");
                    }
                }
                catch
                {
                    sb.AppendLine($"  {ttl}\t*\tTimeout");
                }
            }

            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = task_id,
            });
        }

        private void ExecuteIfconfig(string task_id)
        {
            StringBuilder sb = new StringBuilder();
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                sb.Append(netInterface.Name + Environment.NewLine + Environment.NewLine);
                sb.Append("\tDescription: " + netInterface.Description + Environment.NewLine + Environment.NewLine);
                IPInterfaceProperties ipProps = netInterface.GetIPProperties();
                int i = 0;

                foreach (UnicastIPAddressInformation unicastIPAddressInformation in netInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (i == 0)
                        {
                            sb.Append("\tSubnet Mask: " + unicastIPAddressInformation.IPv4Mask + Environment.NewLine);
                        }
                        else
                        {
                            sb.Append("\t\t\t" + unicastIPAddressInformation.IPv4Mask + Environment.NewLine);
                        }
                        i++;
                    }
                }
                i = 0;
                sb.Append(Environment.NewLine);

                foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                {
                    if (i == 0)
                    {
                        sb.Append("\t\tAddresses: " + addr.Address.ToString() + Environment.NewLine);
                    }
                    else
                    {
                        sb.Append("\t\t\t" + addr.Address.ToString() + Environment.NewLine);
                    }
                    i++;
                }
                i = 0;
                sb.AppendLine();
                if (ipProps.GatewayAddresses.Count == 0)
                {
                    sb.Append("\tDefault Gateway:" + Environment.NewLine);
                }
                else
                {
                    foreach (var gateway in ipProps.GatewayAddresses)
                    {
                        if (i == 0)
                        {
                            sb.Append("\tDefault Gateway: " + gateway.Address.ToString() + Environment.NewLine);
                        }
                        else
                        {
                            sb.Append("\t\t\t" + gateway.Address.ToString() + Environment.NewLine);
                        }
                    }
                }
                sb.Append(Environment.NewLine + Environment.NewLine + Environment.NewLine);
            }
            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = sb.ToString(),
                task_id = task_id,
            });
        }

        private void ExecuteNetstat(string task_id)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Proto Local Address Foreign Address State PID");
            foreach (TcpRow tcpRow in ManagedIpHelper.GetExtendedTcpTable(true))
            {
                sb.AppendFormat(" {0,-7}{1,-23}{2, -23}{3,-14}{4}", "TCP", tcpRow.LocalEndPoint, tcpRow.RemoteEndPoint, tcpRow.State, tcpRow.ProcessId);
                sb.AppendLine();
            }

            messageManager.AddTaskResponse(new TaskResponse()
            {
                task_id = task_id,
                user_output = sb.ToString(),
                completed = true
            });
        }

        private async Task ExecuteArp(net_enum.NetEnumArgs args, string task_id)
        {
            if (string.IsNullOrWhiteSpace(args.cidr))
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = "cidr is required (e.g. 192.168.1.0/24)",
                    task_id = task_id,
                    status = "error"
                });
                return;
            }

            IPNetwork ipnetwork = IPNetwork.Parse(args.cidr);
            System.Net.IPAddressCollection iac = ipnetwork.ListIPAddress();
            int timeoutMs = args.timeout * 1000;

            DebugLog.Log($"{Name} scanning CIDR {args.cidr} with timeout {args.timeout}s [{task_id}]");

            await Task.Run(() =>
            {
                Parallel.ForEach(iac, ipString =>
                {
                    messageManager.WriteLine(ThreadedARPRequest(ipString.ToString()), task_id, false);
                });
            });

            messageManager.Write("Finished Executing", task_id, true);
        }

        private string MacAddresstoString(byte[] macAdrr)
        {
            string macString = BitConverter.ToString(macAdrr);
            return macString.ToUpper();
        }

        private string ThreadedARPRequest(string ipString)
        {
            byte[] macAddr = new byte[6];

            try
            {
                IPAddress ipAddress = IPAddress.Parse(ipString);
                SendARP((int)BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0), 0, macAddr, ref macAddrLen);
                if (MacAddresstoString(macAddr) != "00-00-00-00-00-00")
                {
                    string macString = MacAddresstoString(macAddr);
                    return $"{ipString} - {macString} - Alive";
                }
            }
            catch (Exception)
            {
                return $"{ipString} - Invalid";
            }
            return "";
        }

        private void ExecuteTestPort(net_enum.NetEnumArgs args, string task_id)
        {
            string[] hosts;

            if (!string.IsNullOrEmpty(args.targetlist))
            {
                hosts = GetTargetsFromFile(Convert.FromBase64String(args.targetlist)).ToArray();
            }
            else
            {
                hosts = args.hosts.Split(',');
            }

            if (hosts.Length < 1)
            {
                messageManager.WriteLine("No targets provided!", task_id, true, "error");
                return;
            }

            string[] ports = args.ports.Split(',');

            DebugLog.Log($"{Name} scanning {hosts.Length} hosts, {ports.Length} ports [{task_id}]");
            Parallel.ForEach(hosts, host =>
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
                messageManager.WriteLine(sb.ToString(), task_id, false);
            });

            messageManager.WriteLine("", task_id, true);
        }

        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);
            return allData.Split(Environment.NewLine);
        }
    }
}
