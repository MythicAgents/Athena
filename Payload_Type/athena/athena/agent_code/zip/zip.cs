using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "zip";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private IAgentConfig agentConfig { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
            this.agentConfig = config;
        }
        IEnumerable<string> GetFiles(string path)
        {
            var queue = new Queue<string>();
            queue.Enqueue(path);

            while (queue.Count > 0)
            {
                path = queue.Dequeue();

                try
                {
                    foreach (var subDir in Directory.GetDirectories(path))
                    {
                        queue.Enqueue(subDir);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
                {
                    // Do nothing
                }

                string[] files = null;
                try
                {
                    files = Directory.GetFiles(path);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
                {
                    // Do nothing
                }

                if (files == null)
                {
                    continue;
                }

                foreach (var t in files)
                {
                    yield return t;
                }
            }
        }
        void DebugWriteLine(string message, string task_id)
        {
            if (agentConfig.debug)
            {
                messageManager.WriteLine(message, task_id, false);
            }
        }
        public async Task Execute(ServerJob job)
        {
            ZipArgs args = JsonSerializer.Deserialize<ZipArgs>(job.task.parameters);
            // Open a memory stream to write our zip into

            if(args == null || !args.Validate())
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to validate arguments",
                    completed = true,
                    status = "error"
                });
            }

            if (Directory.Exists(args.destination))
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Destination already exists",
                    completed = true
                });
                return;
            }

            if (!args.destination.EndsWith(".zip"))
            {
                args.destination = args.destination + ".zip";
            }

            FileStream str = new FileStream(args.destination, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            using var archive = new ZipArchive(str, ZipArchiveMode.Create, true);
            var files = GetFiles(args.source).ToList();
            // For every file in the target folder
            foreach (var filename in files)
            {
                // Open the target file for reading
                FileStream fileStream;
                try
                {
                    DebugWriteLine($"Opening {filename} for reading", job.task.id);
                    fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (FileNotFoundException e)
                {
                    DebugWriteLine($"File not found???", job.task.id);
                    continue;
                }
                catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException
                                              or FileNotFoundException)
                {
                    DebugWriteLine($"Unauthorized access exception received, skipping", args.verbose, job.task.id);
                    continue;
                }

                // Create an entry in the zip file
                var fileEntry = archive.CreateEntry(filename, CompressionLevel.SmallestSize);

                // Open a writer on this new zip file entry
                using var entryStream = fileEntry.Open();
                using var streamWriter = new StreamWriter(entryStream);

                // Copy the contents of this file into the zip entry
                try
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                    fileStream.CopyTo(streamWriter.BaseStream);
                }
                catch (Exception e) when (e is IOException)
                {
                    DebugWriteLine($"Error reading file '{filename}':{Environment.NewLine}{e.ToString()}", job.task.id);
                }
            }
            // If we have nothing to write, let's bounce
            if (str.Length == 0)
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Something caused the stream to fail.",
                    completed = true
                });
                return;
            }


            await messageManager.AddResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = $"{str.Length} bytes written to {args.destination}.",
                completed = true
            });
            return;
        }
    }
}
