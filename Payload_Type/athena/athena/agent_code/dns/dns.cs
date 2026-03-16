using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "dns";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<dns.DnsArgs>(
                job.task.parameters);

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
                string result = args.action.ToLowerInvariant() switch
                {
                    "bulk" => BulkResolve(args),
                    _ => await ResolveDns(args),
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id,
                });
            }
            catch (Exception e)
            {
                messageManager.Write(
                    e.ToString(), job.task.id, true, "error");
            }
        }

        private string BulkResolve(dns.DnsArgs args)
        {
            IEnumerable<string> hosts;

            if (!string.IsNullOrEmpty(args.targetlist))
            {
                hosts = GetTargetsFromFile(
                    Misc.Base64DecodeToByteArray(args.targetlist));
            }
            else if (!string.IsNullOrEmpty(args.hosts))
            {
                hosts = args.hosts.Split(',');
            }
            else
            {
                return "No hosts or target list provided.";
            }

            var sb = new StringBuilder();

            foreach (var host in hosts)
            {
                string trimmed = host.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                try
                {
                    if (IPAddress.TryParse(trimmed, out var ipAddr))
                    {
                        sb.AppendLine(ReverseLookup(ipAddr));
                    }
                    else
                    {
                        sb.AppendLine(LookUpByHost(trimmed));
                    }
                }
                catch (Exception)
                {
                    sb.AppendLine($"{trimmed}\t\tNOTFOUND");
                }
            }

            return sb.ToString();
        }

        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);
            return allData.Split(Environment.NewLine);
        }

        private string ReverseLookup(IPAddress ip)
        {
            var sb = new StringBuilder();
            try
            {
                IPHostEntry hostInfo = Dns.GetHostEntry(ip);

                foreach (var alias in hostInfo.Aliases)
                {
                    sb.AppendLine($"{ip}\t\t{alias}");
                }

                if (hostInfo.Aliases.Length == 0)
                {
                    sb.AppendLine($"{ip}\t\t{hostInfo.HostName}");
                }
            }
            catch
            {
                sb.AppendLine($"{ip}\t\tNOTFOUND");
            }

            return sb.ToString();
        }

        private string LookUpByHost(string host)
        {
            var sb = new StringBuilder();
            try
            {
                foreach (var ip in Dns.GetHostEntry(host).AddressList)
                {
                    sb.AppendLine($"{host}\t\t{ip}");
                }
            }
            catch
            {
                sb.AppendLine($"{host}\t\tNOTFOUND");
            }

            return sb.ToString();
        }

        private async Task<string> ResolveDns(dns.DnsArgs args)
        {
            if (string.IsNullOrEmpty(args.hostname))
                throw new ArgumentException("Hostname is required.");

            var sb = new StringBuilder();
            sb.AppendLine(
                $"DNS Lookup: {args.hostname} ({args.record_type})");
            sb.AppendLine(new string('-', 40));

            switch (args.record_type.ToUpperInvariant())
            {
                case "A":
                case "AAAA":
                    var entry =
                        await Dns.GetHostEntryAsync(args.hostname);
                    foreach (var addr in entry.AddressList)
                    {
                        if (args.record_type == "A"
                            && addr.AddressFamily
                                == AddressFamily.InterNetwork)
                            sb.AppendLine($"A\t{addr}");
                        else if (args.record_type == "AAAA"
                            && addr.AddressFamily
                                == AddressFamily.InterNetworkV6)
                            sb.AppendLine($"AAAA\t{addr}");
                        else if (args.record_type == "A")
                            sb.AppendLine(
                                $"{(addr.AddressFamily == AddressFamily.InterNetworkV6 ? "AAAA" : "A")}\t{addr}");
                    }
                    if (entry.HostName != args.hostname)
                        sb.AppendLine($"CNAME\t{entry.HostName}");
                    if (entry.Aliases.Length > 0)
                    {
                        foreach (var alias in entry.Aliases)
                            sb.AppendLine($"Alias\t{alias}");
                    }
                    break;

                case "CNAME":
                    var cnameEntry =
                        await Dns.GetHostEntryAsync(args.hostname);
                    if (cnameEntry.HostName != args.hostname)
                        sb.AppendLine($"CNAME\t{cnameEntry.HostName}");
                    else
                        sb.AppendLine("No CNAME record found.");
                    break;

                case "PTR":
                    if (IPAddress.TryParse(
                        args.hostname, out var ip))
                    {
                        var ptrEntry =
                            await Dns.GetHostEntryAsync(ip);
                        sb.AppendLine($"PTR\t{ptrEntry.HostName}");
                    }
                    else
                    {
                        sb.AppendLine(
                            "PTR lookup requires an IP address.");
                    }
                    break;

                default:
                    sb.AppendLine(
                        $"Record type '{args.record_type}' requires P/Invoke (not yet implemented). Showing A records:");
                    var fallback =
                        await Dns.GetHostEntryAsync(args.hostname);
                    foreach (var addr in fallback.AddressList)
                        sb.AppendLine($"A\t{addr}");
                    break;
            }

            return sb.ToString();
        }
    }
}
