using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Athena
{
    public static class Plugin
    {
        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            return new PluginResponse()
            {
                success = true,
                output = "Not implemented yet."
            };
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }

    }
}
