using System;
using System.Collections.Generic;
using System.IO;

namespace Athena
{
    public static class Plugin
    {
        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            string output = "{\"Directories\":[";

            try
            {
                string[] directories;
                if (args.ContainsKey("path"))
                {
                    directories = Directory.GetFileSystemEntries((string)args["path"]);
                }
                else
                {
                    directories = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory());
                }
                foreach (var dir in directories)
                {
                    output += $"{{\"path\":\"{dir}\",\"LastAccessTime\":\"{Directory.GetLastAccessTime(dir)}\",\"LastWriteTime\":\"{Directory.GetLastWriteTime(dir)}\",\"CreationTime\",\"{Directory.GetCreationTime(dir)}\"}},";
                }
                output = output.TrimEnd(',');
                output += "]}";
                return new PluginResponse()
                {
                    success = true,
                    output = output
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
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
