using Microsoft.Win32;
using PluginBase;
using System.Security;
using System.Text;

namespace Plugin
{
    public class reg
    {
        public static string NormalizeKey(string text)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>()
            {
                {"HKEY_LOCAL_MACHINE\\","HKLM\\" },
                {"HKEY_CURRENT_USER\\", "HKCU\\" },
                {"HKEY_USERS\\", "HKU\\" },
                {"HKEY_CURRENT_CONFIG\\", "HKCC\\" },

            };

            foreach(var item in dic)
            {
                if (text.StartsWith(item.Key))
                {
                    return text.Replace(item.Key, item.Value);
                }
            }

            return text; //Return value if none match
        }
        public static void Execute(Dictionary<string, object> args)
        {
            string action = (string)args["action"];
            string keyPath = NormalizeKey((string)args["keypath"]);
            ResponseResult rr = new ResponseResult()
            {
                task_id = (string)args["task-id"],
                completed = "true",
            };


            bool error = false;

            switch (action)
            {
                case "query":
                    rr.user_output = RegistryQuery(keyPath, (string)args["hostname"], out error);
                    break;
                case "add":
                    rr.user_output = RegistryAdd((string)args["keyname"], keyPath, (string)args["keyvalue"], (string)args["hostname"], out error);
                    break;
                case "delete":
                    rr.user_output = RegistryDelete(keyPath, (string)args["keypath"], (string)args["hostname"], out error);
                    break;
            }

            if (error)
            {
                rr.status = "error";
            }

            PluginHandler.AddResponse(rr);
        }
        static string RegistryDelete(string KeyName, string RegkeyName, string RemoteAddr, out bool error)
        {
            StringBuilder sb = new StringBuilder();
            ResponseResult rr = new ResponseResult();
            try
            {
                //open hive dependent on string
                RegistryKey rk;
                if (KeyName.Split('\\')[0] == "HKCU")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.CurrentUser.OpenSubKey(KeyName, true); // true makes it writeable
                    }
                    else
                    {
                        sb.AppendLine("[*] - You Can't Remotely Query HKCC"); //End of Wednesday
                        error = true;
                        return sb.ToString();
                    }
                    {
                        if (rk == null)
                        {
                            sb.AppendLine("[*] - No Key Found");

                        }
                        else
                        {

                            rk.DeleteValue(RegkeyName, false); //
                            sb.AppendFormat("[*] - Deleted", KeyName).AppendLine();
                        }
                    }
                }
                else if (KeyName.Split('\\')[0] == "HKLM")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.LocalMachine.OpenSubKey(KeyName, true);
                    }
                    else
                    {
                        rk = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, RemoteAddr).CreateSubKey(KeyName);
                    }
                    {
                        if (rk == null)
                        {
                            sb.AppendLine("[*] - No Key Found");

                        }
                        else
                        {
                            rk.DeleteValue(RegkeyName, false);
                            sb.AppendFormat("[*] - Deleted", KeyName).AppendLine();
                        }
                    }
                }
                else if (KeyName.Split('\\')[0] == "HKCC")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.CurrentConfig.OpenSubKey(KeyName, true);
                    }
                    else
                    {
                        sb.AppendLine("[*] - You Can't Remotely Query HKCC"); //End of Wednesday
                        error = true;
                        return sb.ToString();
                        //rk = RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentConfig, RemoteAddr).CreateSubKey(KeyName); 
                    }
                    {
                        if (rk == null)
                        {
                            sb.AppendLine("[*] - No Key Found");

                        }
                        else
                        {
                            rk.DeleteValue(RegkeyName, false);
                            sb.AppendFormat("[*] - Deleted", KeyName).AppendLine();
                        }
                    }
                }
                else if (KeyName.Split('\\')[0] == "HKU")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.Users.OpenSubKey(KeyName, true);
                    }
                    else
                    {
                        rk = RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, RemoteAddr).CreateSubKey(KeyName);
                    }
                    {
                        if (rk == null)
                        {
                            sb.AppendLine("[*] - No Key Found");

                        }
                        else
                        {
                            rk.DeleteValue(RegkeyName, false);
                            sb.AppendFormat("[*] - Deleted", KeyName).AppendLine();
                        }
                    }

                }
                else
                {
                    sb.AppendLine("[*] - No HKey Found");
                }
            }
            catch (SecurityException)
            {
                sb.AppendLine("[*] - Access Denied to Key");
            }
            catch (IOException)
            {
                sb.AppendLine("[*] - Key has been marked for deletion");
            }
            catch
            {
                sb.AppendLine("[*] - Key is not valid");
            }
            error = false;
            return sb.ToString();
        }
        static string RegistryAdd(string KeyName, string keyPath, string KeyValue, string RemoteAddr, out bool error)
        {
            StringBuilder sb = new StringBuilder();
            RegistryKey rk;
            error = false;
            try
            {
                switch (keyPath.Split('\\')[0])
                {
                    case "HKCU":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.CurrentUser.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, RemoteAddr).CreateSubKey(keyPath);
                        //rk = Registry.CurrentUser.CreateSubKey(keyPath);
                        break;
                    case "HKU":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.Users.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, RemoteAddr).CreateSubKey(keyPath);
                        //rk = Registry.Users.CreateSubKey(keyPath);
                        break;
                    case "HKCC":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.CurrentConfig.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentConfig, RemoteAddr).CreateSubKey(keyPath);
                        //rk = Registry.CurrentConfig.CreateSubKey(keyPath);
                        break;
                    case "HKLM":
                        rk = string.IsNullOrEmpty(RemoteAddr) ? Registry.LocalMachine.CreateSubKey(keyPath) :
                            RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, RemoteAddr).CreateSubKey(keyPath);
                        //rk = Registry.LocalMachine.CreateSubKey(keyPath);
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
            catch (SecurityException)
            {
                sb.AppendLine("[*] - Access Denied to Key");
                error = true;
            }
            catch (IOException)
            {
                sb.AppendLine("[*] - Key has been marked for deletion / Permissions Error");
                error = true;
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
                error = true;
            }
            return sb.ToString();
        }
        static string RegistryQuery(string KeyName, string RemoteAddr, out bool error)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                //open hive dependent on string
                RegistryKey rk;
                if (KeyName.Split('\\')[0] == "HKCU")
                {

                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.CurrentUser.OpenSubKey(KeyName);
                    }
                    else
                    {
                        sb.AppendLine("[*] - This Shouldn't Work As u cant query HKCU remotely");
                        error = true;
                        return sb.ToString();
                    }
                    sb.AppendFormat("Main Key: {0}", rk).AppendLine();
                    string[] KeyNames = rk.GetValueNames();
                    // string[] KeyType = rk.GetType();
                    sb.AppendFormat("{0} ------- {1} -------- {2}", "Name", "Type", "Data").AppendLine();
                    foreach (var Subkey in KeyNames) // var = type ambiguous
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
                else if (KeyName.Split('\\')[0] == "HKU")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.Users.OpenSubKey(KeyName);
                    }
                    else
                    {
                        rk = RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, RemoteAddr).OpenSubKey(KeyName);
                    }
                    //
                    sb.AppendFormat("Main Key: {0}", rk).AppendLine();
                    string[] KeyNames = rk.GetValueNames();
                    // string[] KeyType = rk.GetType();
                    sb.AppendFormat("{0} ------- {1} -------- {2}", "Name", "Type", "Data").AppendLine();
                    foreach (var Subkey in KeyNames) // var = type ambiguous
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
                else if (KeyName.Split('\\')[0] == "HKCC")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.CurrentConfig.OpenSubKey(KeyName);
                    }
                    else
                    {
                        sb.AppendLine("[*] - This Shouldn't Work As u cant query HKCC remotely");
                        error = true;
                        return sb.ToString();
                        //rk = RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentConfig, RemoteAddr).OpenSubKey(KeyName);
                    }
                    sb.AppendFormat("Main Key: {0}", rk).AppendLine();
                    string[] KeyNames = rk.GetValueNames();
                    // string[] KeyType = rk.GetType();
                    sb.AppendFormat("{0} ------- {1} -------- {2}", "Name", "Type", "Data").AppendLine();
                    foreach (var Subkey in KeyNames) // var = type ambiguous
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
                else if (KeyName.Split('\\')[0] == "HKLM")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.LocalMachine.OpenSubKey(KeyName);
                    }
                    else
                    {
                        rk = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, RemoteAddr).OpenSubKey(KeyName);
                    }
                    sb.AppendFormat("Main Key: {0}", rk).AppendLine();
                    string[] KeyNames = rk.GetValueNames();
                    // string[] KeyType = rk.GetType();
                    sb.AppendFormat("{0} ------- {1} -------- {2}", "Name", "Type", "Data").AppendLine();
                    foreach (var Subkey in KeyNames) // var = type ambiguous
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
                else
                {
                    sb.AppendLine("[*] - Error - No Type of HK selected");
                    error = true;
                    return sb.ToString();

                }
            }
            catch (SecurityException)
            {
                sb.AppendLine("[*] - Access Denied to Key, Permissions Error");
                error = true;
                return sb.ToString();
            }
            catch (IOException)
            {
                sb.AppendLine("[*] - Machine Name not found");
                error = true;
                return sb.ToString();
            }
            catch
            {
                sb.AppendLine("[*] - Key is not valid");
                error = true;
                return sb.ToString();
            }
            error = false;
            return sb.ToString();
        }
        private static string PrintByteArray(byte[] Bytes)
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