using Agent.Interfaces;
using System.Text;
using System.Text.Json;
using System.Net;
using System.DirectoryServices.Protocols;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {

        private LdapConnection ldapConnection;
        private string domain;
        public string Name => "ds";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            DsArgs args = JsonSerializer.Deserialize<DsArgs>(job.task.parameters);
            string action = args.action;


            switch (action.ToLower())
            {
                case "query":
                    Query(args, job.task.id);
                    break;
                case "connect":
                    Connect(args, job.task.id);
                    break;
                case "disconnect":
                    Disconnect(job.task.id);
                    break;
                case "set":
                    Set(args, job.task.id);
                    break;
                default:
                    messageManager.WriteLine("No valid command specified", job.task.id, true, "error");
                    break;
            }
        }
        static string GetBaseDN(string domain)
        {
            return "DC=" + domain.Replace(".", ",DC=");
        }

        void Set(DsArgs args, string task_id)
        {
            messageManager.WriteLine("Not implemented yet!", task_id, true, "error");
        }

        void Connect(DsArgs args, string task_id)
        {
            LdapDirectoryIdentifier directoryIdentifier;

            if (!string.IsNullOrEmpty(args.domain))
            {
                domain = args.domain;
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
                    messageManager.WriteLine("Failed to identify domain, please specify using the domain switch", task_id, true, "error");
                    return;
                }
            }

            if (string.IsNullOrEmpty(args.server))
            {
                directoryIdentifier = new LdapDirectoryIdentifier(args.server);
            }
            else
            {
                directoryIdentifier = new LdapDirectoryIdentifier(domain);
            }


            if(!string.IsNullOrEmpty(args.username) && args.password != null)
            {
                NetworkCredential cred = new NetworkCredential();
                cred.UserName = args.username;
                cred.Password = args.password;
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
                messageManager.WriteLine($"Successfully bound to LDAP at {domain}", task_id, true);
            }
            catch (Exception e)
            {
                messageManager.WriteLine(e.ToString(), task_id, true, "error");
            }
        }

        void Disconnect(string task_id)
        {
            ldapConnection.Dispose();
            messageManager.WriteLine("Connection Disposed", task_id, true);
        }

        void Query(DsArgs args, string task_id)
        {
            if (ldapConnection is null)
            {
                messageManager.WriteLine("No active LDAP connection, try running ds connect first.", task_id, true, "error");
            }

            StringBuilder sb = new StringBuilder();
            string searchBase;
            string ldapFilter = "";
            string[] properties;

            if (!string.IsNullOrEmpty(args.searchbase))
            {
                searchBase = args.searchbase;
            }
            else
            {
                searchBase = GetBaseDN(domain);
            }

            if (!string.IsNullOrEmpty(args.ldapfilter))
            {
                ldapFilter = args.ldapfilter;
            }

            if (!string.IsNullOrEmpty(args.objectcategory))
            {
                switch(args.objectcategory)
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
                }
            }

            if (string.IsNullOrEmpty(args.properties))
            {
                properties = args.properties.Split(',');
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

                messageManager.WriteLine(JsonSerializer.Serialize(response.Entries), task_id, true);
            }
            catch (LdapException e)
            {
                messageManager.WriteLine(e.ToString(), task_id, true, "error");
            }
            catch (Exception e)
            {
                messageManager.WriteLine(e.ToString(), task_id, true, "error");
            }
        }
    }
}
