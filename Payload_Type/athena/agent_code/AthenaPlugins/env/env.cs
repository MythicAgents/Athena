using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            StringBuilder output = new StringBuilder();
            output.Append("[");
            foreach (DictionaryEntry fs in Environment.GetEnvironmentVariables())
            {
  
                output.Append($"{{\"Name\":\"{fs.Key.ToString().Replace(@"\", @"\\")}\",\"Value\":\"{fs.Value.ToString().Replace(@"\", @"\\")}\"}}" + ","); // Get values in JSON styling and escape \
            }
            output.Remove(output.Length-1,1); // remove extra Comma
            output.Append("]"); // add ending array
            return new PluginResponse()
            {
                success = true,
                output = output.ToString()
            };

        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
