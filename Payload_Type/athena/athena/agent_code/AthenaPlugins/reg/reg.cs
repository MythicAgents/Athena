using Microsoft.Win32;
using Athena.Commands.Models;
using System.Security;
using System.Text;
using Athena.Models;
using Athena.Commands;
using Athena.Models.Responses;

namespace Plugins
{
    public class Reg : AthenaPlugin
    {
        public override string Name => "reg";
        public string NormalizeKey(string text)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>()
            {
                {"HKEY_LOCAL_MACHINE\\","HKLM\\" },
                {"HKEY_CURRENT_USER\\", "HKCU\\" },
                {"HKEY_USERS\\", "HKU\\" },
                {"HKEY_CURRENT_CONFIG\\", "HKCC\\" },

            };

            foreach (var item in dic)
            {
                if (text.StartsWith(item.Key))
                {
                    return text.Replace(item.Key, item.Value);
                }
            }

            return text;
        }
        public override void Execute(Dictionary<string, string> args)
        {
            string action = args["action"];
            string keyPath = NormalizeKey(args["keypath"]);
            ResponseResult rr = new ResponseResult()
            {
                task_id = args["task-id"],
                completed = true,
            };


            bool error = false;

            switch (action)
            {
                case "query":
                    rr.user_output = RegistryQuery(keyPath, args["hostname"], out error);
                    break;
                case "add":
                    rr.user_output = RegistryAdd(args["keyname"], keyPath, args["keyvalue"], args["hostname"], out error);
                    break;
                case "delete":
                    rr.user_output = RegistryDelete(keyPath, args["keyname"], args["hostname"], out error);
                    break;
                default:
                    rr.user_output = "No valid command specified.";
                    error = true;
                    break;
            }

            if (error)
            {
                rr.status = "error";
            }

            TaskResponseHandler.AddResponse(rr);
        }
        string RegistryDelete(string keyPath, string keyName, string RemoteAddr, out bool error)
        {
            StringBuilder sb = new StringBuilder();
            ResponseResult rr = new ResponseResult();
            RegistryKey rk;
            string hive = keyPath.Split('\\')[0];
            keyPath = keyPath.Replace(hive, "").TrimStart('\\');
            error = false;

            try
            {
                switch (hive)
                {
                    case "HKCU":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.CurrentUser.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, RemoteAddr).CreateSubKey(keyPath);
                        break;
                    case "HKU":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.Users.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, RemoteAddr).CreateSubKey(keyPath);
                        break;
                    case "HKCC":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.CurrentConfig.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentConfig, RemoteAddr).CreateSubKey(keyPath);
                        break;
                    case "HKLM":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.LocalMachine.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, RemoteAddr).CreateSubKey(keyPath);
                        break;
                    default:
                        sb.AppendLine("[*] - No valid Key Found");
                        error = true;
                        return sb.ToString();
                }

                if (rk == null)
                {
                    sb.AppendLine("[*] - No valid Key Found");
                    error = true;
                    return sb.ToString();
                }


                rk.DeleteValue(keyName, true);
                sb.AppendLine("[*] - Key Deleted.");
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
                sb.AppendLine(keyName);
                sb.AppendLine(keyPath);
                sb.AppendLine(RemoteAddr);
                error = true;
            }
            return sb.ToString();
        }
        string RegistryAdd(string KeyName, string keyPath, string KeyValue, string RemoteAddr, out bool error)
        {
            StringBuilder sb = new StringBuilder();
            RegistryKey rk;
            string hive = keyPath.Split('\\')[0];
            keyPath = keyPath.Replace(hive, "").TrimStart('\\');
            error = false;
            try
            {
                switch (hive)
                {
                    case "HKCU":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.CurrentUser.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, RemoteAddr).CreateSubKey(keyPath);
                        break;
                    case "HKU":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.Users.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, RemoteAddr).CreateSubKey(keyPath);
                        break;
                    case "HKCC":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.CurrentConfig.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentConfig, RemoteAddr).CreateSubKey(keyPath);
                        break;
                    case "HKLM":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.LocalMachine.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, RemoteAddr).CreateSubKey(keyPath);
                        break;
                    default:
                        sb.AppendLine("[*] - No valid Key Found");
                        error = true;
                        return sb.ToString();
                }



                rk.SetValue(KeyName, KeyValue);

                sb.AppendLine("[*] - Key Added");
                return sb.ToString();

            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
                sb.AppendLine(KeyName);
                sb.AppendLine(keyPath);
                sb.AppendLine(KeyValue);
                sb.AppendLine(RemoteAddr);
                error = true;
            }
            return sb.ToString();
        }
        string RegistryQuery(string keyPath, string RemoteAddr, out bool error)
        {
            StringBuilder sb = new StringBuilder();
            error = false;

            try
            {
                //open hive dependent on string
                RegistryKey rk;

                switch (keyPath.Split('\\')[0])
                {
                    case "HKCU":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.CurrentUser.OpenSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, RemoteAddr).OpenSubKey(keyPath);
                        break;
                    case "HKU":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.Users.OpenSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, RemoteAddr).OpenSubKey(keyPath);
                        break;
                    case "HKCC":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.CurrentConfig.OpenSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentConfig, RemoteAddr).OpenSubKey(keyPath);
                        break;
                    case "HKLM":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.LocalMachine.OpenSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, RemoteAddr).OpenSubKey(keyPath);
                        break;
                    default:
                        sb.AppendLine("[*] - No valid Key Found");
                        error = true;
                        return sb.ToString();
                }
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
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
                sb.AppendLine(keyPath);
                sb.AppendLine(RemoteAddr);
                error = true;
            }
            return sb.ToString();
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