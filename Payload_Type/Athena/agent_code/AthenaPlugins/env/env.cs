using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {                                  
            string output = "";
            foreach (DictionaryEntry fs in Environment.GetEnvironmentVariables())
            {
                output += (fs.Key.ToString() + " = " + fs.Value.ToString() + Environment.NewLine);
            }
            return new PluginResponse()
            {
                success = true,
                output = output
            };

        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
