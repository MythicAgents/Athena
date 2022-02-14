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
                    FileAttributes attr = File.GetAttributes((string)args["source"]);

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        // Copy Directory to new location recursively
                        if (!CopyDirectory((string)args["source"], (string)args["destination"], true))
                        {
                            return new PluginResponse()
                            {
                                success = false,
                                output = string.Format("Failed to copy Directory: {0}", (string)args["source"])
                            };
                        }
                    }
                    else
                    {
                        // Copy file
                        File.Copy((string)args["source"], (string)args["destination"]);
                    }
                    return new PluginResponse()
                    {
                        success = true,
                        output = string.Format("Successfully Copied {0} tp {1}", (string)args["source"], (string)args["destination"])
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
        static bool CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                return false;

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
            return true;
        }
        public class PluginResponse
        {
            public bool success { get; set; }
            public string output { get; set; }
        }
    }
}
