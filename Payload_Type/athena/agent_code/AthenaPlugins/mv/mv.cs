using System.IO;
using System;
using System.Collections.Generic;

namespace Athena
{
    public static class Plugin
    {

        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            if (args.ContainsKey("source") && args.ContainsKey("destination"))
            {
                try
                {
                    FileAttributes attr = File.GetAttributes((string)args["source"]);

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Move((string)args["source"], (string)args["destination"]);
                    }
                    else
                    {
                        File.Move((string)args["source"], (string)args["destination"]);
                    }
                    return new PluginResponse()
                    {
                        success = true,
                        output = String.Format("Moved {0} tp {1}", (string)args["source"], (string)args["destination"])
                    };
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
            else
            {
                return new PluginResponse()
                {
                    success = false,
                    output = "Please specify both a source and destination for the file!"
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
