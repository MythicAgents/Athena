using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;
namespace Athena
{
    public static class Plugin
    {

        public static ResponseResult Execute(Dictionary<string, object> args)
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
                            return new ResponseResult
                            {
                                completed = "true",
                                user_output = $"Failed to copy {(string)args["source"]} to {(string)args["destination"]}",
                                task_id = (string)args["task-id"],
                                status = "error"
                            };
                        }
                    }
                    else
                    {
                        // Copy file
                        File.Copy((string)args["source"], (string)args["destination"]);
                    }
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = $"Copied {(string)args["source"]} to {(string)args["destination"]}",
                        task_id = (string)args["task-id"],
                    };
                }
                else
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = $"Missing required parameters",
                        task_id = (string)args["task-id"],
                        status = "error"
                    };
                }
            }
            catch (Exception e)
            {
                return new ResponseResult
                {
                    completed = "true",
                    user_output = $"Failed to copy {(string)args["source"]} to {(string)args["destination"]}{Environment.NewLine}{e.Message}",
                    task_id = (string)args["task-id"],
                    status = "error"
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
    }
}
