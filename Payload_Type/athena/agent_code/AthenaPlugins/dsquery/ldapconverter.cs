using System.DirectoryServices.Protocols;
using System.Text;

namespace dsquery
{
    internal static class ldapconverter
    {
        internal static string ConvertLDAPProperty(SearchResultEntry Result)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string PropertyName in Result.Attributes.AttributeNames)
            {
                if (Result.Attributes[PropertyName].Count == 0) { continue; }
                if (PropertyName == "objectsid")
                {
                    sb.Append("\"" + PropertyName + "\":\"" + BitConverter.ToString((byte[])Result.Attributes["objectsid"][0], 0) + "\",");
                }
                else if (PropertyName == "sidhistory")
                {
                    List<string> historyListTemp = new List<string>();
                    foreach (byte[] bytes in Result.Attributes["sidhistory"])
                    {
                        historyListTemp.Add(BitConverter.ToString(bytes));
                    }
                    sb.Append("\"" + PropertyName + "\":\"" + historyListTemp.ToArray() + "\",");
                }
                else if (PropertyName == "grouptype")
                {
                    try {
                        sb.Append("\"" + PropertyName + "\":\"" + (GroupTypeEnum)Enum.Parse(typeof(GroupTypeEnum), Result.Attributes["grouptype"][0].ToString()) + "\",");
                    }
                    catch (Exception e)
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + e.Message + "\",");
                    }
                }
                else if (PropertyName == "samaccounttype")
                {
                    try
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + (SamAccountTypeEnum)Enum.Parse(typeof(SamAccountTypeEnum), Result.Attributes["samaccounttype"][0].ToString()) + "\",");
                    }
                    catch (Exception e)
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + e.Message + "\",");
                    }
                }
                else if (PropertyName == "objectguid")
                {
                    sb.Append("\"" + PropertyName + "\":\"" + Result.Attributes["objectguid"][0] + "\",");
                }
                else if (PropertyName == "useraccountcontrol")
                {
                    try
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + (UACEnum)Enum.Parse(typeof(UACEnum), Result.Attributes["useraccountcontrol"][0].ToString()) + "\",");
                    }
                    catch (Exception e)
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + e.Message + "\",");
                    }
                }
                else if (PropertyName == "ntsecuritydescriptor")
                {
                }
                else if (PropertyName == "accountexpires")
                {
                    try
                    {
                        if (long.Parse(Result.Attributes["accountexpires"][0].ToString()) >= DateTime.MaxValue.Ticks)
                        {
                            sb.Append("\"" + PropertyName + "\":\"" + DateTime.MaxValue.ToString() + "\",");
                        }
                        //sb.Append("\"" + PropertyName + "\":\"" + DateTime.FromFileTime(long.Parse(Result.Attributes[PropertyName][0].ToString())).ToString() + "\",");
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + DateTime.MaxValue.ToString() + "\",");
                    }
                }
                else if (PropertyName == "lastlogon" || PropertyName == "lastlogontimestamp" || PropertyName == "pwdlastset" ||
                         PropertyName == "lastlogoff" || PropertyName == "badPasswordTime")
                {
                    DateTime dateTime = DateTime.MinValue;
                    if (Result.Attributes[PropertyName][0].GetType().Name == "System.MarshalByRefObject")
                    {
                        var comobj = (MarshalByRefObject)Result.Attributes[PropertyName][0];
                        int high = (int)comobj.GetType().InvokeMember("HighPart", System.Reflection.BindingFlags.GetProperty, null, comobj, null);
                        int low = (int)comobj.GetType().InvokeMember("LowPart", System.Reflection.BindingFlags.GetProperty, null, comobj, null);
                        dateTime = DateTime.FromFileTime(long.Parse("0x" + high + "" + low, System.Globalization.NumberStyles.HexNumber));
                    }
                    else
                    {
                        dateTime = DateTime.FromFileTime(long.Parse(Result.Attributes[PropertyName][0].ToString()));

                    }
                    if (PropertyName == "lastlogon")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + dateTime.ToString() + "\",");
                    }
                    else if (PropertyName == "lastlogontimestamp")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + dateTime.ToString() + "\",");
                    }
                    else if (PropertyName == "pwdlastset")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + dateTime.ToString() + "\",");
                    }
                    else if (PropertyName == "lastlogoff")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + dateTime.ToString() + "\",");
                    }
                    else if (PropertyName == "badPasswordTime")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + dateTime.ToString() + "\",");
                    }
                }
                else
                {
                    string property = "0";
                    if (Result.Attributes[PropertyName][0].GetType().Name == "System.MarshalByRefObject")
                    {
                        var comobj = (MarshalByRefObject)Result.Attributes[PropertyName][0];
                        int high = (int)comobj.GetType().InvokeMember("HighPart", System.Reflection.BindingFlags.GetProperty, null, comobj, null);
                        int low = (int)comobj.GetType().InvokeMember("LowPart", System.Reflection.BindingFlags.GetProperty, null, comobj, null);
                        property = int.Parse("0x" + high + "" + low, System.Globalization.NumberStyles.HexNumber).ToString();
                    }
                    else if (Result.Attributes[PropertyName].Count == 1)
                    {
                        property = Result.Attributes[PropertyName][0].ToString();
                    }
                    else
                    {
                        List<string> propertyList = new List<string>();
                        foreach (object prop in Result.Attributes[PropertyName])
                        {
                            propertyList.Add(prop.ToString());
                        }
                        property = String.Join(", ", propertyList.ToArray());
                    }
                    if (PropertyName == "samaccountname")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + property + "\",");
                    }
                    else if (PropertyName == "distinguishedname")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + property + "\",");
                    }
                    else if (PropertyName == "cn")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + property + "\",");
                    }
                    else if (PropertyName == "admincount")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + property + "\",");
                    }
                    else if (PropertyName == "serviceprincipalname")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + property + "\",");
                    }
                    else if (PropertyName == "name")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + property + "\",");
                    }
                    else if (PropertyName == "description")
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + property + "\",");
                    }
                    else if (PropertyName == "memberof")
                    {
                        StringBuilder sb2 = new StringBuilder();
                        foreach (byte[] group in Result.Attributes[PropertyName])
                        {
                            sb2.Append(System.Text.Encoding.Default.GetString(group) + "|");
                        }
                        sb2.Remove(sb2.Length - 2, 2); //Remove new line characters
                        sb.Append("\"" + PropertyName + "\":\"" + sb2.ToString() + "\",");
                    }
                    else
                    {
                        sb.Append("\"" + PropertyName + "\":\"" + Result.Attributes[PropertyName] + "\",");
                    }
                }
            }
            return sb.ToString();
        }
        public enum NameType
        {
            DN = 1,
            Canonical = 2,
            NT4 = 3,
            Display = 4,
            DomainSimple = 5,
            EnterpriseSimple = 6,
            GUID = 7,
            Unknown = 8,
            UPN = 9,
            CanonicalEx = 10,
            SPN = 11,
            SID = 12
        }
        public enum SamAccountTypeEnum : uint
        {
            DOMAIN_OBJECT = 0x00000000,
            GROUP_OBJECT = 0x10000000,
            NON_SECURITY_GROUP_OBJECT = 0x10000001,
            ALIAS_OBJECT = 0x20000000,
            NON_SECURITY_ALIAS_OBJECT = 0x20000001,
            USER_OBJECT = 0x30000000,
            MACHINE_ACCOUNT = 0x30000001,
            TRUST_ACCOUNT = 0x30000002,
            APP_BASIC_GROUP = 0x40000000,
            APP_QUERY_GROUP = 0x40000001,
            ACCOUNT_TYPE_MAX = 0x7fffffff
        }
        [Flags]
        public enum GroupTypeEnum : uint
        {
            CREATED_BY_SYSTEM = 0x00000001,
            GLOBAL_SCOPE = 0x00000002,
            DOMAIN_LOCAL_SCOPE = 0x00000004,
            UNIVERSAL_SCOPE = 0x00000008,
            APP_BASIC = 0x00000010,
            APP_QUERY = 0x00000020,
            SECURITY = 0x80000000
        }
        [Flags]
        public enum UACEnum : uint
        {
            SCRIPT = 1,
            ACCOUNTDISABLE = 2,
            HOMEDIR_REQUIRED = 8,
            LOCKOUT = 16,
            PASSWD_NOTREQD = 32,
            PASSWD_CANT_CHANGE = 64,
            ENCRYPTED_TEXT_PWD_ALLOWED = 128,
            TEMP_DUPLICATE_ACCOUNT = 256,
            NORMAL_ACCOUNT = 512,
            INTERDOMAIN_TRUST_ACCOUNT = 2048,
            WORKSTATION_TRUST_ACCOUNT = 4096,
            SERVER_TRUST_ACCOUNT = 8192,
            DONT_EXPIRE_PASSWORD = 65536,
            MNS_LOGON_ACCOUNT = 131072,
            SMARTCARD_REQUIRED = 262144,
            TRUSTED_FOR_DELEGATION = 524288,
            NOT_DELEGATED = 1048576,
            USE_DES_KEY_ONLY = 2097152,
            DONT_REQ_PREAUTH = 4194304,
            PASSWORD_EXPIRED = 8388608,
            TRUSTED_TO_AUTH_FOR_DELEGATION = 16777216,
            PARTIAL_SECRETS_ACCOUNT = 67108864
        }
        public enum DomainObjectType
        {
            User,
            Group,
            Computer
        }
    }
}
