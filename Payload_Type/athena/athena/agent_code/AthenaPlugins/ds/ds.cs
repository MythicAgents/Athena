using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using Athena.Commands.Models;
using Athena.Commands;
using System.Text.Json;

namespace Plugins
{
    public class Ds : AthenaPlugin
    {
        public override string Name => "ds";
        static LdapConnection ldapConnection;
        static string domain;

        public override void Execute(Dictionary<string, string> args)
        {
            string action = args["action"];


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
                    TaskResponseHandler.WriteLine("No valid command specified", args["task-id"], true, "error");
                    break;
            }
        }
        static string GetBaseDN(string domain)
        {
            return "DC=" + domain.Replace(".", ",DC=");
        }

        void Set(Dictionary<string, string> args)
        {
            TaskResponseHandler.WriteLine("Not implemented yet!", args["task-id"], true, "error");
        }

        void Connect(Dictionary<string, string> args)
        {

            //if (!OperatingSystem.IsWindows()) //Workaround for https://github.com/dotnet/runtime/issues/60972
            //{
            //    setenv("LDAPTLS_REQCERT", "never");
            //}

            LdapDirectoryIdentifier directoryIdentifier;

            if (args["domain"] != "")
            {
                domain = args["domain"];
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
                    TaskResponseHandler.WriteLine("Failed to identify domain, please specify using the domain switch", args["task-id"], true, "error");
                    return;
                }
            }

            if (args["server"] != "") //Try getting the server first
            {
                directoryIdentifier = new LdapDirectoryIdentifier(args["server"]);
            }
            else
            {
                directoryIdentifier = new LdapDirectoryIdentifier(domain);
            }

            if (args["username"] != "" && args["password"] != "")
            {
                NetworkCredential cred = new NetworkCredential();
                cred.UserName = args["username"];
                cred.Password = args["password"];
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
                TaskResponseHandler.WriteLine($"Successfully bound to LDAP at {domain}", args["task-id"], true);
            }
            catch (Exception e)
            {
                TaskResponseHandler.WriteLine(e.ToString(), args["task-id"], true, "error");
            }
        }

        void Disconnect(Dictionary<string, string> args)
        {
            ldapConnection.Dispose();
            TaskResponseHandler.WriteLine("Connection Disposed", args["task-id"], true);
        }

        void Query(Dictionary<string, string> args)
        {
            if (ldapConnection is null)
            {
                TaskResponseHandler.WriteLine("No active LDAP connection, try running ds connect first.", args["task-id"], true, "error");
            }


            StringBuilder sb = new StringBuilder();
            string searchBase;
            string ldapFilter = "";
            string[] properties;
            if (args["searchbase"] != "")
            {
                searchBase = args["searchbase"];
            }
            else
            {
                searchBase = GetBaseDN(domain);
            }

            if (args["ldapfilter"] != "")
            {
                ldapFilter = args["ldapfilter"];
            }

            if (args["objectcategory"] != "")
            {
                switch (args["objectcategory"])
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
                            ldapFilter = "(*=*)";
                        }
                        break;
                };
            }

            if (args["properties"] != "")
            {
                properties = (args["properties"]).Split(',');
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

                TaskResponseHandler.WriteLine(JsonSerializer.Serialize(response.Entries), args["task-id"], true);
            }
            catch (LdapException e)
            {
                TaskResponseHandler.WriteLine(e.ToString(), args["task-id"], true, "error");
            }
            catch (Exception e)
            {
                TaskResponseHandler.WriteLine(e.ToString(), args["task-id"], true, "error");
            }
        }
    }
}
