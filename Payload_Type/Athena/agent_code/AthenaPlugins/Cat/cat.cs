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
                if (args.ContainsKey("path"))
                {
                    return new PluginResponse()
                    {
                        success = true,
                        output = File.ReadAllText(args["path"].ToString().Replace("\"",""))
                    };
                }
                else
                {
                    return new PluginResponse()
                    {
                        success = false,
                        output = "A path needs to be specified"
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

