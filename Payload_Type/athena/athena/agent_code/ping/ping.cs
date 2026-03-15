using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "ping";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            var args = JsonSerializer.Deserialize<ping.PingArgs>(job.task.parameters);

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
                string result = args.action switch
                {
                    "ping" => await ExecutePing(args),
                    "traceroute" => await ExecuteTraceroute(args),
                    _ => throw new ArgumentException($"Unknown action: {args.action}")
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
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private async Task<string> ExecutePing(ping.PingArgs args)
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
            return sb.ToString();
        }

        private async Task<string> ExecuteTraceroute(ping.PingArgs args)
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

            return sb.ToString();
        }
    }
}
