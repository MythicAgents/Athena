using Agent.Interfaces;
using Agent.Models;

namespace pwd
{
    public class Pwd : IPlugin
    {
        public string Name => "pwd";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public Pwd(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            await messageManager.AddResponse(new ResponseResult
            {
                completed = true,
                user_output = Directory.GetCurrentDirectory(),
                task_id = job.task.id,
            });
        }
    }
}
