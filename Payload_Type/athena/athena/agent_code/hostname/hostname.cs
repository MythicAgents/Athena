using System;
using System.Collections.Generic;
using System.Net;
using Agent.Models;
using Agent.Interfaces;


namespace hostname
{
    public class HostName : IPlugin
    {
        public string Name => "hostname";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }

        public HostName(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
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
                user_output = Dns.GetHostName(),
                task_id = job.task.id,
            });
        }
    }
}
