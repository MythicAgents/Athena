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
            string keyName = NormalizeKey((string)args["keypath"]);
            ResponseResult rr = new ResponseResult()
            {
                task_id = (string)args["task-id"],
                completed = "true",
            };


            bool error = false;

            switch (action)
            {
                case "query":
                    rr.user_output = RegistryQuery(keyName, (string)args["hostname"], out error);
                    break;
                case "add":
                    rr.user_output = RegistryAdd(keyName, (string)args["keypath"], (string)args["keyvalue"], (string)args["hostname"], out error);
                    break;
                case "delete":
                    rr.user_output = RegistryDelete(keyName, (string)args["keypath"], (string)args["hostname"], out error);
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
        static string RegistryAdd(string KeyName, string RegkeyName, string RegkeyValue, string RemoteAddr, out bool error)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                RegistryKey rk;
                if (KeyName.Split('\\')[0] == "HKCU")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.CurrentUser.CreateSubKey(KeyName);
                        rk.SetValue(RegkeyName, RegkeyValue);
                        sb.AppendLine("[*] - Key Added");
                    }
                    else
                    {
                        sb.AppendLine("[*] - Can't Query Remotely");
                        error = true;
                        return sb.ToString();
                    }

                }
                else if (KeyName.Split('\\')[0] == "HKU")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.Users.CreateSubKey(KeyName);
                    }
                    else
                    {
                        rk = RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, RemoteAddr).CreateSubKey(KeyName);
                    }
                    rk.SetValue(RegkeyName, RegkeyValue);
                    sb.AppendLine("[*] - Key Added");
                }
                else if (KeyName.Split('\\')[0] == "HKCC")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.CurrentConfig.CreateSubKey(KeyName);
                        rk.SetValue(RegkeyName, RegkeyValue);
                        sb.AppendLine("[*] - Key Added");

                    }
                    else
                    {
                        sb.AppendLine("[*] - Can't Query Current Config Remotely "); // what is the create remote
                        error = true;
                        return sb.ToString();
                    }

                }
                else if (KeyName.Split('\\')[0] == "HKLM")
                {
                    KeyName = KeyName.Replace(KeyName.Split('\\')[0], "").TrimStart('\\');
                    if (string.IsNullOrEmpty(RemoteAddr)) //check for Remote
                    {
                        rk = Registry.LocalMachine.CreateSubKey(KeyName);
                    }
                    else
                    {
                        rk = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, RemoteAddr).CreateSubKey(KeyName);
                    }
                    rk.SetValue(RegkeyName, RegkeyValue);
                    sb.AppendLine("[*] - Key Added");
                }
                else
                {
                    sb.AppendLine("[*] - No HK Selected");
                    error = true;
                    return sb.ToString();
                }

            }
            catch (SecurityException)
            {
                sb.AppendLine("[*] - Access Denied to Key");
                error = true;
                return sb.ToString();

            }
            catch (IOException)
            {
                sb.AppendLine("[*] - Key has been marked for deletion / Permissions Error");
                error = true;
                return sb.ToString();
            }
            catch
            {
                sb.AppendLine("[*] - Key is not valid, Or Error due to permissions");
                error = true;
                return sb.ToString();
            }
            error = false;
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