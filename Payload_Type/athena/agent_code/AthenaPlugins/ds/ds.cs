using System.DirectoryServices.Protocols;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using Athena.Plugins;
using Plugin;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "ds";
        static LdapConnection ldapConnection;
        static string domain;

        public override void Execute(Dictionary<string, object> args)
        {
            string action = (string)args["action"];


            switch (action.ToLower())
            {
                case "query":
                    Query(args);
                    break;
                case "connect":
                    Connect(args);
                    break;
                case "disconnect":
                    Disconnect(args);
                    break;
                case "set":
                    Set(args);
                    break;
                default:
                    PluginHandler.WriteLine("No valid command specified", (string)args["task-id"], true, "error");
                    break;
            }
        }
        static string GetBaseDN(string domain)
        {
            return "DC=" + domain.Replace(".", ",DC=");
        }

        void Set(Dictionary<string, object> args)
        {
            PluginHandler.WriteLine("Not implemented yet!", (string)args["task-id"], true, "error");
        }

        void Connect(Dictionary<string, object> args)
        {

            //if (!OperatingSystem.IsWindows()) //Workaround for https://github.com/dotnet/runtime/issues/60972
            //{
            //    setenv("LDAPTLS_REQCERT", "never");
            //}

            LdapDirectoryIdentifier directoryIdentifier;

            if ((string)args["domain"] != "")
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
                    PluginHandler.WriteLine("Failed to identify domain, please specify using the domain switch", (string)args["task-id"], true, "error");
                    return;
                }
            }

            if ((string)args["server"] != "") //Try getting the server first
            {
                directoryIdentifier = new LdapDirectoryIdentifier((string)args["server"]);
            }
            else
            {
                directoryIdentifier = new LdapDirectoryIdentifier(domain);
            }

            if ((string)args["username"] != "" && (string)args["password"] != "")
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
                PluginHandler.WriteLine($"Successfully bound to LDAP at {domain}", (string)args["task-id"], true);
            }
            catch (Exception e)
            {
                PluginHandler.WriteLine(e.ToString(), (string)args["task-id"], true, "error");
            }
        }

        void Disconnect(Dictionary<string, object> args)
        {
            ldapConnection.Dispose();
            PluginHandler.WriteLine("Connection Disposed", (string)args["task-id"], true);
        }

        void Query(Dictionary<string, object> args)
        {
            if (ldapConnection is null)
            {
                PluginHandler.WriteLine("No active LDAP connection, try running ds connect first.", (string)args["task-id"], true, "error");
            }


            StringBuilder sb = new StringBuilder();
            string searchBase;
            string ldapFilter = "";
            string[] properties;
            if ((string)args["searchbase"] != "")
            {
                searchBase = (string)args["searchbase"];
            }
            else
            {
                searchBase = GetBaseDN(domain);
            }

            if ((string)args["ldapfilter"] != "")
            {
                ldapFilter = (string)args["ldapfilter"];
            }

            if ((string)args["objectcategory"] != "")
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
                    default: //This also encompasses *
                        if (string.IsNullOrEmpty(ldapFilter))
                        {
                            ldapFilter = "()";
                        }
                        break;
                };
            }

            if ((string)args["properties"] != "")
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
                PluginHandler.WriteLine(sb.ToString(), (string)args["task-id"], true);
            }
            catch (Exception e)
            {
                PluginHandler.WriteLine(e.ToString(), (string)args["task-id"], true, "error");
            }
        }
    }
}
