using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Athena
{
    public static class Plugin
    {
        public static PluginResponse Execute(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");

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
                    sb.Append($"{{\"path\":\"{dir.Replace(@"\",@"\\")}\",\"LastAccessTime\":\"{Directory.GetLastAccessTime(dir)}\",\"LastWriteTime\":\"{Directory.GetLastWriteTime(dir)}\",\"CreationTime\":\"{Directory.GetCreationTime(dir)}\"}},");
                }

                sb.Remove(sb.Length - 1, 1);
                sb.Append("]");
                return new PluginResponse()
                {
                    success = true,
                    output = sb.ToString()
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
