﻿using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text;
using System.Text.Json;

namespace config
{
    public class Config : IPlugin
    {
        public string Name => "config";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Config(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            ConfigUpdateArgs args = JsonSerializer.Deserialize<ConfigUpdateArgs>(job.task.parameters);
            if(args is null)
            {
                await messageManager.Write("Invalid parameters", job.task.id, true, "error");
                return;
            }


            try
            {
                StringBuilder sb = new StringBuilder();

                if(args.sleep >= 0)
                {
                    config.sleep = args.sleep;
                    sb.AppendLine($"Updated sleep interval to {config.sleep}");
                }

                if(args.sleep >= 0)
                {
                    config.jitter = args.jitter;
                    sb.AppendLine($"Updated jitter interval to {config.jitter}");
                }

                if(!String.IsNullOrEmpty(args.killdate))
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

                await messageManager.Write(sb.ToString(), job.task.id, true, "");

            }
            catch (Exception e)
            {
                await messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}