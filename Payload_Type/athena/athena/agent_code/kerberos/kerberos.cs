using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "kerberos";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    messageManager.Write(
                        "Kerberos operations are only available on Windows",
                        job.task.id, true, "error");
                    return;
                }

                var args = JsonSerializer.Deserialize<kerberos.KerberosArgs>(
                    job.task.parameters) ?? new kerberos.KerberosArgs();

                string result = args.action switch
                {
                    "klist" => ListTickets(),
                    "purge" => PurgeTickets(),
                    _ => throw new ArgumentException(
                        $"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id
                });
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        private string ListTickets()
        {
            try
            {
                uint lsaHandle = 0;
                int status = LsaConnectUntrusted(ref lsaHandle);
                if (status != 0)
                    return $"LsaConnectUntrusted failed: 0x{status:X8}";

                LsaDeregisterLogonProcess(lsaHandle);
                return "Kerberos ticket listing via SSPI - use klist for details";
            }
            catch (Exception e)
            {
                return $"Error listing tickets: {e.Message}";
            }
        }

        private string PurgeTickets()
        {
            return "Kerberos ticket purge via SSPI is not yet implemented";
        }

        [DllImport("secur32.dll")]
        private static extern int LsaConnectUntrusted(ref uint LsaHandle);

        [DllImport("secur32.dll")]
        private static extern int LsaDeregisterLogonProcess(uint LsaHandle);
    }
}
