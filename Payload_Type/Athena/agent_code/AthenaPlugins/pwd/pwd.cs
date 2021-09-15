using System;
using System.Collections.Generic;
using System.IO;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            return new PluginResponse()
            {
                success = true,
                output = Directory.GetCurrentDirectory()
            };
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
