using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using GetDomainUsers;

namespace Athena
{
    public static class Plugin
    {
        //We can pass dictionaries to functions. I just need to figure out how I want to do it on the agent side.
        public static PluginResponse Execute(Dictionary<string,object> args)
        {
            try
            {
                string output = "[";
                string SearchBase;
                string[] properties;
                LdapConnection ldapConnection;
                LdapDirectoryIdentifier directoryIdentifier;
                NetworkCredential cred = new NetworkCredential();

                //Connect to a specific DC, or find one 
                if (args.ContainsKey("server"))
                {
                    directoryIdentifier = new LdapDirectoryIdentifier((string)args["server"]);
                }
                else
                {
                    directoryIdentifier = new LdapDirectoryIdentifier((string)args["domain"]);
                }
                //Use provided searchbase or create one based on the domain 
                if (args.ContainsKey("searchbase"))
                {
                    SearchBase = (string)args["searchbase"];
                }
                else
                {
                    SearchBase = "CN=Users," + GetBaseDN((string)args["domain"]);
                }
                if (args.ContainsKey("user"))
                {
                    if (args.ContainsKey("password"))
                    {
                        cred.UserName = (string)args["user"];
                        cred.Password = (string)args["password"];
                    }
                    else
                    {
                        return new PluginResponse()
                        {
                            success = false,
                            output = "Credentials need to be specified!"
                        };
                    }
                }
                else
                {
                    return new PluginResponse()
                    {
                        success = false,
                        output = "Credentials need to be specified!"
                    };
                }
                if (args.ContainsKey("properties"))
                {
                    properties = ((string)args["properties"]).Split(',');
                }
                else
                {
                    properties = new string[] { "samaccountname", "description", "lastlogontimestamp", "pwdlastset", "serviceprincipalname" };
                }

                ldapConnection = new LdapConnection(directoryIdentifier, cred);

                List<DomainObject> DomainUsers = CoreSploit.GetDomainUsers(ldapConnection, SearchBase, Properties: properties);

                foreach (var user in DomainUsers)
                {
                    output += "{";
                    foreach (var prop in properties)
                    {
                        output += "\"" + prop + "\",\"" + user.GetType().GetProperty(prop) + "\"";
                    }
                    output += "},";
                }

                output = output.TrimEnd(',');
                output += "]";

                return new PluginResponse()
                {
                    success = true,
                    output = output
                };
            }
            catch (Exception e)
            {
                return new PluginResponse()
                {
                    success = true,
                    output = e.Message
                };
            }
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
        public static string GetBaseDN(string domain)
        {
            return "DC=" + domain.Replace(".", ",DC=");
        }

    }
}
