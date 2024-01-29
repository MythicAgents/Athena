using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "cp";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            CopyArgs args = JsonSerializer.Deserialize<CopyArgs>(job.task.parameters);
            try
            {
                if(string.IsNullOrEmpty(args.source) || string.IsNullOrEmpty(args.destination))
                {
                    messageManager.Write("Missing required parameters", job.task.id, true, "error");
                    return;
                }   


                string source = args.source.Replace("\"", "");
                string destination = args.destination.Replace("\"", "");

                FileAttributes attr = File.GetAttributes(source);

                // Check if Directory
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    // Copy Directory to new location recursively
                    if (!CopyDirectory(source, destination, true))
                    {
                        messageManager.Write($"Failed to copy {source} to {destination}", job.task.id, true, "error");
                    }
                }
                else
                {
                    // Copy file
                    File.Copy(source, destination);
                }

                messageManager.Write($"Copied {source} to {destination}", job.task.id, true, "");
 
            }
            catch (Exception e)
            {
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
