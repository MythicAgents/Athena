using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Text;
using GetDomainUsers;

namespace Athena
{
    public static class Plugin
    {
        //We can pass dictionaries to functions. I just need to figure out how I want to do it on the agent side.
        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("[");
                //string output = "[";
                string SearchBase;
                string[] properties;
                LdapConnection ldapConnection;
                LdapDirectoryIdentifier directoryIdentifier;
                NetworkCredential cred = new NetworkCredential();
                //Connect to a specific DC, or find one 
                if (args.ContainsKey("server") && !String.IsNullOrEmpty((string)args["server"]))
                {
                    directoryIdentifier = new LdapDirectoryIdentifier((string)args["domain"]);
                    ldapConnection = new LdapConnection((string)args["server"]);
                }
                else
                {
                    directoryIdentifier = new LdapDirectoryIdentifier((string)args["domain"]);
                    ldapConnection = new LdapConnection((string)args["domain"]);
                }

                //Use provided searchbase or create one based on the domain 
                if (args.ContainsKey("searchbase") && !String.IsNullOrEmpty((string)args["searchbase"]))
                {
                    SearchBase = (string)args["searchbase"];
                }
                else
                {
                    SearchBase = GetBaseDN((string)args["domain"]);
                }

                if (args.ContainsKey("user") && !String.IsNullOrEmpty((string)args["user"]))
                {
                    if (args.ContainsKey("password") && !String.IsNullOrEmpty((string)args["password"]))
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
                    if (args.ContainsKey("domain"))
                    {
                        cred.Domain = (string)args["domain"];
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
                if (args.ContainsKey("properties") && !String.IsNullOrEmpty((string)args["properties"]))
                {
                    properties = ((string)args["properties"]).Split(',');
                }
                else
                {
                    properties = new string[] { "samaccountname", "description", "lastlogontimestamp", "pwdlastset", "serviceprincipalname" };
                }

                ldapConnection.Credential = cred;

                List<DomainObject> DomainUsers = CoreSploit.GetDomainUsers(ldapConnection, SearchBase, Properties: properties);

                foreach (var user in DomainUsers)
                {
                    sb.Append("{");
                    if (properties[0] == "*" || properties[0].ToLower() == "all")
                    {
                        foreach (var prop in user.GetType().GetProperties())
                        {
                            sb.Append("\"" + prop.Name + "\":\"" + user.GetType().GetProperty(prop.Name).GetValue(user) + "\",");
                        }
                    }
                    else
                    {
                        foreach (var prop in properties)
                        {
                            try
                            {
                                if (user.GetType().GetProperty(prop) is not null)
                                {
                                    string val = "";
                                    if(user.GetType().GetProperty(prop).GetValue(user) is null)
                                    {
                                        val = "";
                                    }
                                    else
                                    {
                                        val = user.GetType().GetProperty(prop).GetValue(user).ToString().Replace(@"\", @"\\").Replace("\"","\\\"");
                                    }

                                    sb.Append("\"" + prop + "\":\"" + val + "\",");
                                }
                                else
                                {
                                    sb.Append("\"" + prop + "\":\"" + "Property doesn't exist" + "\",");
                                }
                            }
                            catch
                            {
                                sb.Append("\"" + prop + "\":\"" + "" + "\",");
                            }
                        }
                    }
                    sb.Remove(sb.Length - 1, 1);
                    sb.Append("},");
                }
                sb.Remove(sb.Length - 1, 1);
                sb.Append("]");

                return new PluginResponse()
                {
                    success = true,
                    output = sb.ToString()
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
