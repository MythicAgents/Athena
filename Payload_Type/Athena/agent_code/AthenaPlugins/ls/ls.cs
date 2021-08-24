using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Athena
{
    public static class Plugin
    {
        public static string Execute(Dictionary<string, object> args)
        {
            try
            {
                string output = "{\"Directories\":[";
                string[] directories;
                foreach(var arg in args)
                {
                    Console.WriteLine(arg.Key);
                    Console.WriteLine(arg.Value);
                }
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
                return output;
            }
            catch
            {
                return "";
            }
            return output;
        }
    }
}
