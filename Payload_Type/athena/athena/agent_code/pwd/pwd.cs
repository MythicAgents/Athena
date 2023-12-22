﻿using Agent.Interfaces;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "pwd";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
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
