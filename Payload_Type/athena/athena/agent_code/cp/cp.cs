using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "cp";
        private IDataBroker messageManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.tokenManager = context.TokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            CopyArgs args = JsonSerializer.Deserialize<CopyArgs>(job.task.parameters);
            try
            {
                if(string.IsNullOrEmpty(args.source) || string.IsNullOrEmpty(args.destination))
                {
                    DebugLog.Log($"{Name} missing required parameters [{job.task.id}]");
                    messageManager.Write("Missing required parameters", job.task.id, true, "error");
                    return;
                }


                string source = args.source.Replace("\"", "");
                string destination = args.destination.Replace("\"", "");

                FileAttributes attr = File.GetAttributes(source);

                // Check if Directory
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    DebugLog.Log($"{Name} copying directory {source} to {destination} [{job.task.id}]");
                    // Copy Directory to new location recursively
                    if (!CopyDirectory(source, destination, true))
                    {
                        DebugLog.Log($"{Name} directory copy failed [{job.task.id}]");
                        messageManager.Write($"Failed to copy {source} to {destination}", job.task.id, true, "error");
                    }
                }
                else
                {
                    DebugLog.Log($"{Name} copying file {source} to {destination} [{job.task.id}]");
                    // Copy file
                    File.Copy(source, destination);
                }

                messageManager.Write($"Copied {source} to {destination}", job.task.id, true, "");
                DebugLog.Log($"{Name} completed [{job.task.id}]");
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error [{job.task.id}]: {e.Message}");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
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
