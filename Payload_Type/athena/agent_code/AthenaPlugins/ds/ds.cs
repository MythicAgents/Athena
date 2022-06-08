using System.DirectoryServices.Protocols;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using PluginBase;

namespace Plugin
{
    public static class ds
    {
        //[DllImport("libc")]
        //public static extern void setenv(string name, string value);

        //[DllImport("libc")]
        //public static extern void unsetenv(string name);

        static LdapConnection ldapConnection;
        static string domain;

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            string action = (string)args["action"];


            switch (action.ToLower())
            {
                case "query":
                    return Query(args);
                    break;
                case "connect":
                    return Connect(args);
                    break;
                case "disconnect":
                    return Disconnect(args);
                    break;
            }

            return new ResponseResult
            {
                task_id = (string)args["task-id"],
                user_output = $"No valid command specified.",
                completed = "true",
                status = "error"
            };
        }

        static string GetBaseDN(string domain)
        {
            return "DC=" + domain.Replace(".", ",DC=");
        }

        static ResponseResult Connect(Dictionary<string, object> args)
        {

            //if (!OperatingSystem.IsWindows()) //Workaround for https://github.com/dotnet/runtime/issues/60972
            //{
            //    setenv("LDAPTLS_REQCERT", "never");
            //}

            //Todo: Persistent ldap bind?

            LdapDirectoryIdentifier directoryIdentifier;

            if (checkHasValue("domain", args))
            {
                domain = (string)args["domain"];
            }
            else
            {
                if (OperatingSystem.IsWindows())
                {
                    domain = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
                }
                else if (OperatingSystem.IsLinux())
                {
                    domain = Environment.GetEnvironmentVariable("DOMAIN");
                }

                if (string.IsNullOrEmpty(domain))
                {
                    return new ResponseResult
                    {
                        user_output = $"Failed to identify domain, please specify using the domain switch",
                        completed = "true",
                        task_id = (string)args["task-id"],
                        status = "error"
                    };
                }
            }

            if (checkHasValue("server", args)) //Try getting the server first
            {
                directoryIdentifier = new LdapDirectoryIdentifier((string)args["server"]);
            }
            else
            {
                directoryIdentifier = new LdapDirectoryIdentifier(domain);
            }

            if (checkHasValue("username", args) && checkHasValue("password", args))
            {
                NetworkCredential cred = new NetworkCredential();
                cred.UserName = (string)args["username"];
                cred.Password = (string)args["password"];
                cred.Domain = domain;
                ldapConnection = new LdapConnection(directoryIdentifier, cred); // Credentialed Context
            }
            else
            {
                ldapConnection = new LdapConnection(directoryIdentifier); // Default Context
            }

            try
            {
                ldapConnection.Bind();

                return new ResponseResult
                {
                    user_output = $"Successfully bound to LDAP at {domain} with default SearchBase: {GetBaseDN(domain)}",
                    completed = "true",
                    task_id = (string)args["task-id"]
                };
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    user_output = e.ToString(),
                    completed = "true",
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }

        static ResponseResult Disconnect(Dictionary<string, object> args)
        {
            ldapConnection.Dispose();
            return new ResponseResult
            {
                user_output = "Connection Disposed",
                completed = "true",
                task_id = (string)args["task-id"],
            };
        }

        static ResponseResult Query(Dictionary<string, object> args)
        {
            if (ldapConnection is null)
            {
                return new ResponseResult
                {
                    task_id = (string)args["task-id"],
                    user_output = $"No LDAP connection specified, use connect!.",
                    completed = "true",
                    status = "error"
                };
            }


            StringBuilder sb = new StringBuilder();
            string searchBase;
            string ldapFilter = "";
            string[] properties;
            if (checkHasValue("searchbase", args))
            {
                searchBase = (string)args["searchbase"];
            }
            else
            {
                searchBase = GetBaseDN(domain);
            }

            if (checkHasValue("ldapfilter", args))
            {
                ldapFilter = (string)args["ldapfilter"];
            }

            if (checkHasValue("objectcategory", args))
            {
                switch ((string)args["objectcategory"])
                {
                    case "user":
                        ldapFilter = $"(&(samAccountType=805306368){ldapFilter})";
                        break;
                    case "group":
                        ldapFilter = $"(&(objectCategory=group){ldapFilter})";
                        break;
                    case "ou":
                        ldapFilter = $"(&(objectCategory=organizationalUnit){ldapFilter})";
                        break;
                    case "computer":
                        ldapFilter = $"(&(samAccountType=805306369){ldapFilter})";
                        break;
                    case "*":
                        if (string.IsNullOrEmpty(ldapFilter))
                        {
                            ldapFilter = "()";
                        }
                        break;
                    default:
                        if (string.IsNullOrEmpty(ldapFilter))
                        {
                            ldapFilter = "()";
                        }
                        break;
                };
            }

            if (checkHasValue("properties", args))
            {
                properties = ((string)args["properties"]).Split(',');
            }
            else
            {
                properties = new string[] { "cn", "description" };
            }

            try
            {
                SearchRequest request;
                if (properties[0] == "*" || properties[0] == "all")
                {
                    request = new SearchRequest(searchBase, ldapFilter, SearchScope.Subtree, null);
                }
                else
                {
                    request = new SearchRequest(searchBase, ldapFilter, SearchScope.Subtree, properties);
                }

                SearchResponse response = (SearchResponse)ldapConnection.SendRequest(request);

                sb.Append("{\"results\": [");
                if (response.Entries.Count > 0)
                {
                    foreach (SearchResultEntry entry in response.Entries)
                    {
                        sb.Append("{");
                        sb.Append(ldapconverter.ConvertLDAPProperty(entry));
                        sb.Remove(sb.Length - 1, 1);
                        sb.Append("},");
                    }
                    sb.Remove(sb.Length - 1, 1);
                    sb.Append("] }");
                }
                else
                {
                    sb.Append("]}");
                }

                return new ResponseResult
                {
                    user_output = sb.ToString(),
                    completed = "true",
                    task_id = (string)args["task-id"],
                };
            }
            catch (Exception e)
            {
                StringBuilder sb2 = new StringBuilder();
                sb.AppendLine($"Domain: {domain}" ?? "NULL");
                sb.AppendLine($"SearchBase: {searchBase}" ?? "NULL");
                sb.AppendLine($"LdapFilter: {ldapFilter}" ?? "NULL");
                sb.AppendLine($"Properties: {string.Join(",",properties)}" ?? "NULL");
                sb.Append(e.ToString());
                return new ResponseResult
                {
                    user_output = sb.ToString(),
                    completed = "true",
                    status = "error",
                    task_id = (string)args["task-id"],
                };
            }
        }

        private static bool checkHasValue(string valueName, Dictionary<string,object> args)
        {
            if(args.ContainsKey(valueName) && !string.IsNullOrEmpty(valueName))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
