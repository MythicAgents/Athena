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

        private LdapConnection? ldapConnection;
        private string domain = string.Empty;
        public string Name => "ds";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            DsArgs args = JsonSerializer.Deserialize<DsArgs>(job.task.parameters);
            if(args is null){

            }

            var actions = new Dictionary<string, Action>
            {
                { "query", () => Query(args, job.task.id) },
                { "connect", () => Connect(args, job.task.id) },
                { "disconnect", () => Disconnect(job.task.id) },
                { "set", () => Set(args, job.task.id) }
            };

            string action = args.action;
            if (actions.TryGetValue(action.ToLower() ?? string.Empty, out var func)){
                func();
            }
            else{
                messageManager.WriteLine("No valid command specified", job.task.id, true, "error");
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
            domain = string.Empty;

            if (!string.IsNullOrEmpty(args.domain))
            {
                domain = args.domain;
            }

            if (OperatingSystem.IsWindows())
            {
                domain = Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? "";
            }

            if (OperatingSystem.IsLinux())
            {
                domain = Environment.GetEnvironmentVariable("DOMAIN") ?? "";
            }

            if (string.IsNullOrEmpty(domain))
            {
                return false;
            }
            return true;
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
            if(ldapConnection is null){
                return;
            }
            ldapConnection.Dispose();
            messageManager.WriteLine("Connection Disposed", task_id, true);
        }

        void Query(DsArgs args, string task_id)
        {
            if (ldapConnection is null)
            {
                messageManager.WriteLine("No active LDAP connection, try running ds connect first.", task_id, true, "error");
                return;
            }

            string searchBase;
            string[] properties = null;
            string ldapFilter = this.ConstructLdapFilter(args);
            
            if (!string.IsNullOrEmpty(args.searchbase))
            {
                searchBase = args.searchbase;
            }
            else
            {
                searchBase = GetBaseDN(domain);
            }


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
            Dictionary<string, string> filters = new(){
                { "user", "(samAccountType=805306368)" },
                { "group", "(samAccountType=268435457)" },
                { "ou","(objectCategory=organizationalUnit)" },
                { "computer","(samAccountType=805306369)" },
                { "*","(objectCategory=*)" },
                { "trust", "(samAccountType=805306370)" }
            };

            string categoryFilter = string.Empty;
            if(filters.ContainsKey(args.objectcategory.ToLower())){
                categoryFilter = filters[args.objectcategory.ToLower()];
            }

            return string.IsNullOrEmpty(args.ldapfilter) ? categoryFilter : $"(&{categoryFilter}{args.ldapfilter})";
        }
    }
}
