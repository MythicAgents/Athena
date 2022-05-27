using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using dsquery;
using PluginBase;

namespace Athena
{
    public static class Plugin
    {
        //We can pass dictionaries to functions. I just need to figure out how I want to do it on the agent side.
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string ldapFilter = "";
                string searchBase;
                string objectCategory;
                string[] properties;
                LdapConnection ldapConnection;
                LdapDirectoryIdentifier directoryIdentifier;
                NetworkCredential cred = new NetworkCredential();

                if(String.IsNullOrEmpty((string)args["username"]) || String.IsNullOrEmpty((string)args["password"]) || String.IsNullOrEmpty((string)args["domain"]))
                {
                    return new ResponseResult
                    {
                        task_id = (string)args["task-id"],
                        user_output = "Credentials not specified",
                        completed = "true",
                        status = "error"
                    };
                }

                cred.UserName = (string)args["username"];
                cred.Password = (string)args["password"];
                cred.Domain = (string)args["domain"];


                if (!String.IsNullOrEmpty((string)args["ldapfilter"]))
                {
                    ldapFilter = (string)args["ldapfilter"];
                }

                if (!String.IsNullOrEmpty((string)args["objectcategory"]))
                {
                    switch ((string)args["objectcategory"])
                    {
                        case "user":
                            ldapFilter = "(&(samAccountType=805306368)" + ldapFilter + ")";
                            break;
                        case "group":
                            ldapFilter = "(&(objectCategory=group)" + ldapFilter + ")";
                            break;
                        case "ou":
                            ldapFilter = "(&(objectCategory=organizationalUnit)" + ldapFilter + ")";
                            break;
                        case "computer":
                            ldapFilter = "(&(samAccountType=805306369)" + ldapFilter + ")";
                            break;
                        case "*":
                            if (string.IsNullOrEmpty(ldapFilter))
                            {
                                ldapFilter = null;
                            }
                            break;
                    };
                }

                if (!String.IsNullOrEmpty((string)args["searchbase"]))
                {
                    searchBase = (string)args["searchbase"];
                }
                else
                {
                    searchBase = GetBaseDN((string)args["domain"]);
                }

                if (!String.IsNullOrEmpty((string)args["server"]))
                {
                    directoryIdentifier = new LdapDirectoryIdentifier((string)args["domain"]);
                    ldapConnection = new LdapConnection((string)args["server"]);
                }
                else
                {
                    directoryIdentifier = new LdapDirectoryIdentifier((string)args["domain"]);
                    ldapConnection = new LdapConnection((string)args["domain"]);
                }

                if (!String.IsNullOrEmpty((string)args["properties"]))
                {
                    properties = ((string)args["properties"]).Split(',');
                }
                else
                {
                    properties = new string[] { "samaccountname", "description", "lastlogontimestamp", "pwdlastset", "serviceprincipalname" };
                }

                ldapConnection.Credential = cred;


                try
                {
                    SearchRequest request;
                    if(properties[0] == "*" || properties[0] == "all")
                    {
                        request = new SearchRequest(searchBase, ldapFilter, SearchScope.Subtree, null);
                    }
                    else
                    {
                        request = new SearchRequest(searchBase, ldapFilter, SearchScope.Subtree, properties);
                    }

                    SearchResponse response = (SearchResponse)ldapConnection.SendRequest(request);

                    sb.Append("{\"results\": [");
                    if(response.Entries.Count > 0)
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
                }
                catch (Exception e)
                {
                    return new ResponseResult
                    {
                        user_output = e.ToString(),
                        completed = "true",
                        status = "error",
                        task_id = (string)args["task-id"],
                    };
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
                return new ResponseResult
                {
                    user_output = e.ToString(),
                    completed = "true",
                    status = "error",
                    task_id = (string)args["task-id"],
                };
            }
        }
        public static string GetBaseDN(string domain)
        {
            return "DC=" + domain.Replace(".", ",DC=");
        }

    }
}
