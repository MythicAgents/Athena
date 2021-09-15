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
                    DirectoryInfo dir = Directory.CreateDirectory((string)args["path"]);
                    return new PluginResponse()
                    {
                        success = true,
                        output = "Created directory " + dir.FullName
                    };
                }
                else
                {
                    return new PluginResponse()
                    {
                        success = false,
                        output = "Please specify a directory to create!"
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
