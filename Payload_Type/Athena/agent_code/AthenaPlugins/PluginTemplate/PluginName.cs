using System;
using System.Collections.Generic;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            if (args.ContainsKey("myparameter"))
            {
                return new PluginResponse()
                {
                    success = true,
                    output = "Found the parameter!"
                };
            }

            if (args.ContainsKey("message"))
            {
                return new PluginResponse()
                {
                    success = true,
                    output = (string)args["message"]
                };
            }

            return new PluginResponse()
            {
                success = false,
                output = "Couldn't find any parameters"
            };
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }

}
