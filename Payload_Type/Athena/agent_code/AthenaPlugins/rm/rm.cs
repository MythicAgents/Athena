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
                if (args.ContainsKey("file"))
                {
                    File.Delete((string)args["file"]);
                    return new PluginResponse()
                    {
                        success = true,
                        output = "Deleted File: " + (string)args["file"]
                    };
                }
                else
                {
                    return new PluginResponse()
                    {
                        success = false,
                        output = "Please specify a file to delete!"
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
