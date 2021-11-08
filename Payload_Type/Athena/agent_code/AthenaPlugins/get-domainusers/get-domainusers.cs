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
        public static PluginResponse Execute(Dictionary<string, object> args)
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
                        //cred.Domain = (string)args["domain"];
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

                Console.WriteLine("creating ldap connection");
                //directoryIdentifier = new LdapDirectoryIdentifier("meteor.gaia.local");
                //ldapConnection = new LdapConnection("meteor.gaia.local");
                ldapConnection.Credential = cred;

                List<DomainObject> DomainUsers = CoreSploit.GetDomainUsers(ldapConnection, SearchBase, Properties: properties);

                foreach (var user in DomainUsers)
                {
                    output += "{";
                    if (properties[0] == "*" || properties[0].ToLower() == "all")
                    {
                        foreach (var prop in user.GetType().GetProperties())
                        {
                            output += "\"" + prop.Name + "\",\"" + user.GetType().GetProperty(prop.Name).GetValue(user) + "\"";
                        }
                    }
                    else
                    {
                        foreach (var prop in properties)
                        {
                            try
                            {
                                if (user.GetType().GetProperty(prop) != null)
                                {
                                    output += "\"" + prop + "\",\"" + user.GetType().GetProperty(prop).GetValue(user) + "\"";
                                }
                                else
                                {
                                    output += "\"" + prop + "\",\"" + "Property doesn't exist" + "\"";
                                }
                            }
                            catch
                            {
                                output += "\"" + prop + "\",\"" + "" + "\"";
                            }
                        }
                    }

                    output += "}," + Environment.NewLine;
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
