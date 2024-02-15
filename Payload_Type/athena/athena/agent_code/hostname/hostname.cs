using System;
using System.Collections.Generic;
using System.Net;
using Agent.Models;
using Agent.Interfaces;


namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "hostname";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            await messageManager.AddResponse(new TaskResponse
            {
                completed = true,
                user_output = Dns.GetHostName(),
                task_id = job.task.id,
            });
        }
    }
}
