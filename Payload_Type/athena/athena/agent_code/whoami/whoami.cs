using Agent.Interfaces;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "whoami";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
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
