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
                if (args.ContainsKey("directory"))
                {
                    if(args.ContainsKey("force") && (string)args["force"] == "true")
                    {
                        Directory.Delete((string)args["directory"],true);
                        return new PluginResponse()
                        {
                            success = true,
                            output = "Deleted Directory and all sub files and folders in: " + (string)args["directory"]
                        };
                    }
                    else
                    {
                        Directory.Delete((string)args["directory"]);
                        return new PluginResponse()
                        {
                            success = true,
                            output = "Deleted Directory: " + (string)args["directory"]
                        };
                    }
                }
                else
                {
                    return new PluginResponse()
                    {
                        success = false,
                        output = "Please specify a directory to delete."
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
