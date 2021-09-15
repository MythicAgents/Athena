using System;
using System.Collections.Generic;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            return new PluginResponse()
            {
                success = true,
                output = Environment.UserDomainName + "\\" + Environment.UserName
            };
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
