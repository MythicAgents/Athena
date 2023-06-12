﻿using Athena.Commands;
using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
namespace Plugins
{
    public class Cp : AthenaPlugin
    {
        public override string Name => "cp";
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                if (args.ContainsKey("source") && args.ContainsKey("destination"))
                {
                    string source = args["source"].Replace("\"", "");
                    string destination = args["destination"].Replace("\"", "");
                    FileAttributes attr = File.GetAttributes(source);

                    // Check if Directory
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        // Copy Directory to new location recursively
                        if (!CopyDirectory(source, destination, true))
                        {
                            TaskResponseHandler.Write($"Failed to copy {source} to {destination}", args["task-id"], true, "error");
                        }
                    }
                    else
                    {
                        // Copy file
                        File.Copy(source, destination);
                    }

                    TaskResponseHandler.Write($"Copied {source} to {destination}", args["task-id"], true, "");
                }
                else
                {
                    TaskResponseHandler.Write("Missing required parameters", args["task-id"], true, "error");
                }
            }
            catch (Exception e)
            {
                TaskResponseHandler.Write(e.ToString(), args["task-id"], true, "error");
            }
        }
        private bool CopyDirectory(string sourceDir, string destinationDir, bool recursive)
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
