using System;
using System.Collections.Generic;
using System.IO;
namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            try
            {
                if(args.ContainsKey("source") && args.ContainsKey("destination"))
                {
                    File.Copy((string)args["source"], (string)args["destination"]);
                    return new PluginResponse()
                    {
                        success = true,
                        output = string.Format("Copied {0} tp {1}", (string)args["source"], (string)args["destination"])
                    };
                }
                else
                {
                    return new PluginResponse()
                    {
                        success = false,
                        output = "Please specify both a source and destination for the file!"
                    };
                }
            }
            catch (Exception e)
            {
                return new PluginResponse()
                {
                    success = false,
                    output = e.Message
                };
            }
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
