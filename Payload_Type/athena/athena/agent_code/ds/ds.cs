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
            string[] domainComponents = domain.Split('.');
            return string.Join(",", domainComponents.Select(component => $"DC={component}"));
        }

        void Set(DsArgs args, string task_id)
        {
            messageManager.WriteLine("Not implemented yet!", task_id, true, "error");
        }

        void Connect(DsArgs args, string task_id)
        {
            LdapDirectoryIdentifier directoryIdentifier;


            if (!this.TryGetDomain(args, out domain))
            {
                messageManager.WriteLine("Failed to identify domain, please specify using the domain switch", task_id, true, "error");
                return;
            }

            directoryIdentifier = this.GetLdapDirectoryIdentifier(args);

            ldapConnection = this.GetLdapConnection(args, directoryIdentifier);

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
        bool TryGetDomain(DsArgs args, out string domain)
        {
            if (!string.IsNullOrEmpty(args.domain))
            {
                domain = args.domain;
                return true;
            }

            if (OperatingSystem.IsWindows())
            {
                domain = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
                return true;
            }

            if (OperatingSystem.IsLinux())
            {
                domain = Environment.GetEnvironmentVariable("DOMAIN");
                return true;
            }

            domain = "";
            return false;
        }
        LdapConnection GetLdapConnection(DsArgs args, LdapDirectoryIdentifier directoryIdentifier)
        {
            if (!string.IsNullOrEmpty(args.username) && !string.IsNullOrEmpty(args.password))
            {
                return new LdapConnection(directoryIdentifier, new NetworkCredential(args.username, args.password, domain)); // Credentialed Context
            }
            else
            {
                return new LdapConnection(directoryIdentifier); // Default Context
            }
        }
        LdapDirectoryIdentifier GetLdapDirectoryIdentifier(DsArgs args)
        {
            if (string.IsNullOrEmpty(args.server))
            {
                return new LdapDirectoryIdentifier(args.server);
            }
            else
            {
                return new LdapDirectoryIdentifier(domain);
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
            string[] properties = null;

            if (!string.IsNullOrEmpty(args.searchbase))
            {
                searchBase = args.searchbase;
            }
            else
            {
                searchBase = GetBaseDN(domain);
            }

            ldapFilter = this.ConstructLdapFilter(args);


            if (!string.IsNullOrEmpty(args.properties))
            {
                properties = args.properties.Split(',');
            }

            try
            {
                SearchRequest request = new SearchRequest(searchBase, ldapFilter, SearchScope.Subtree, properties);
                request.TimeLimit = TimeSpan.FromSeconds(120);
                SearchResponse response = (SearchResponse)ldapConnection.SendRequest(request);
                messageManager.WriteLine(JsonSerializer.Serialize(response.Entries), task_id, true);
            }
            catch (Exception e)
            {
                messageManager.WriteLine(e.ToString(), task_id, true, "error");
            }
        }

        private string ConstructLdapFilter(DsArgs args)
        {
            string categoryFilter = String.Empty;

            // Validate and construct category filter
            switch (args.objectcategory.ToLower())
            {
                case "user":
                    categoryFilter = "(samAccountType=805306368)";
                    break;
                case "group":
                    categoryFilter = "(samAccountType=268435457)";
                    break;
                case "ou":
                    categoryFilter = "(objectCategory=organizationalUnit)";
                    break;
                case "computer":
                    categoryFilter = "(samAccountType=805306369)";
                    break;
                case "*":
                    categoryFilter = "(objectCategory=*)";
                    break;
                case "trust":
                    categoryFilter = "(samAccountType=805306370)";
                    break;
                default:
                    throw new ArgumentException("Invalid object category.");
            }

            return string.IsNullOrEmpty(args.ldapfilter) ? categoryFilter : $"(&{categoryFilter}{args.ldapfilter})";
        }
    }
}
