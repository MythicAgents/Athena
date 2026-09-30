using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Text.Json;
using upload;

namespace Agent
{
    public class Plugin : IFilePlugin
    {
        public string Name => "upload";
        private const long MaximumTransferBytes = 1024L * 1024 * 1024;
        private readonly IMessageManager messageManager;
        private readonly IAgentConfig config;
        private readonly ConcurrentDictionary<string, TransferState> transfers = new();

        private sealed class TransferState
        {
            public TransferState(ServerUploadJob job) => Job = job;
            public ServerUploadJob Job { get; }
            public object Gate { get; } = new();
            public FileStream? Stream { get; set; }
            public long BytesWritten { get; set; }
            public bool FileCreated { get; set; }
            public CancellationTokenRegistration CancellationRegistration { get; set; }
        }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.config = config;
        }

        public Task Execute(ServerJob job)
        {
            UploadArgs? args = JsonSerializer.Deserialize<UploadArgs>(job.task.parameters);
            string validation = string.Empty;
            if (args is null || !args.Validate(out validation))
            {
                ReportError(job.task.id, validation);
                return Task.CompletedTask;
            }

            var uploadJob = new ServerUploadJob(job, config.chunk_size)
            {
                path = args.path,
                file_id = args.file,
                task = job.task,
                chunk_num = 1,
                cancellationtokensource = job.cancellationtokensource,
            };
            var state = new TransferState(uploadJob);
            if (!transfers.TryAdd(job.task.id, state))
            {
                ReportError(job.task.id, "failed to add job to tracker");
                return Task.CompletedTask;
            }

            try
            {
                state.Stream = new FileStream(uploadJob.path, FileMode.Create, FileAccess.Write, FileShare.None);
                state.FileCreated = true;
                state.CancellationRegistration = job.cancellationtokensource.Token.Register(
                    static value =>
                    {
                        var (plugin, taskId) = ((Plugin, string))value!;
                        plugin.Abort(taskId, "Cancellation Requested");
                    }, (this, job.task.id));
                if (!transfers.TryGetValue(job.task.id, out TransferState? current) || !ReferenceEquals(current, state))
                    return Task.CompletedTask;

                messageManager.AddTaskResponse(new UploadTaskResponse
                {
                    task_id = job.task.id,
                    upload = new UploadTaskResponseData
                    {
                        chunk_size = uploadJob.chunk_size,
                        chunk_num = uploadJob.chunk_num,
                        file_id = uploadJob.file_id,
                        full_path = uploadJob.path,
                    }
                }.ToJson());
            }
            catch (Exception ex)
            {
                Abort(job.task.id, ex.ToString());
            }
            return Task.CompletedTask;
        }

        public Task HandleNextMessage(ServerTaskingResponse response)
        {
            if (!transfers.TryGetValue(response.task_id, out TransferState? state))
            {
                ReportError(response.task_id, "Failed to get job");
                return Task.CompletedTask;
            }

            string encodedChunk = response.chunk_data ?? string.Empty;
            int maximumEncodedChunkLength = checked(((state.Job.chunk_size + 2) / 3) * 4);
            if (encodedChunk.Length > maximumEncodedChunkLength)
            {
                Abort(response.task_id, "Transfer exceeds the configured size limit.");
                return Task.CompletedTask;
            }

            byte[] chunk;
            try { chunk = Misc.Base64DecodeToByteArray(encodedChunk); }
            catch (FormatException) { Abort(response.task_id, "Invalid chunk data received."); return Task.CompletedTask; }

            string? error = null;
            bool completed = false;
            int nextChunk = 0;
            lock (state.Gate)
            {
                if (!transfers.TryGetValue(response.task_id, out TransferState? current) || !ReferenceEquals(current, state))
                    return Task.CompletedTask;
                ServerUploadJob job = state.Job;
                if (job.cancellationtokensource.IsCancellationRequested) error = "Cancellation Requested";
                else if (response.total_chunks <= 0 || (job.total_chunks != 0 && response.total_chunks != job.total_chunks)) error = "Invalid total chunk count.";
                else if (response.chunk_num != job.chunk_num) error = $"Expected chunk {job.chunk_num}, received {response.chunk_num}.";
                else if (chunk.Length == 0) error = "chunk data was empty.";
                else if (chunk.Length > job.chunk_size || (long)response.total_chunks * job.chunk_size > MaximumTransferBytes || state.BytesWritten + chunk.Length > MaximumTransferBytes) error = "Transfer exceeds the configured size limit.";
                else if (state.Stream is null) error = "No stream available.";
                else
                {
                    job.total_chunks = response.total_chunks;
                    try
                    {
                        state.Stream.Write(chunk, 0, chunk.Length);
                        state.BytesWritten += chunk.Length;
                    }
                    catch (Exception ex) { error = ex.ToString(); }
                    if (error is null)
                    {
                        completed = response.chunk_num == job.total_chunks;
                        if (completed)
                        {
                            transfers.TryRemove(response.task_id, out _);
                            state.Stream.Dispose();
                            state.Stream = null;
                        }
                        else nextChunk = ++job.chunk_num;
                    }
                }
            }

            if (error is not null) { Abort(response.task_id, error); return Task.CompletedTask; }
            if (completed)
            {
                state.CancellationRegistration.Unregister();
                messageManager.AddTaskResponse(new UploadTaskResponse
                {
                    task_id = response.task_id,
                    upload = new UploadTaskResponseData { file_id = state.Job.file_id, full_path = state.Job.path },
                    completed = true
                }.ToJson());
                messageManager.CompleteJob(response.task_id);
            }
            else
            {
                messageManager.AddTaskResponse(new UploadTaskResponse
                {
                    task_id = response.task_id,
                    status = GetStatusBar(response.chunk_num, state.Job.total_chunks),
                    upload = new UploadTaskResponseData
                    {
                        chunk_num = nextChunk,
                        file_id = state.Job.file_id,
                        chunk_size = state.Job.chunk_size,
                        full_path = state.Job.path
                    }
                }.ToJson());
            }
            return Task.CompletedTask;
        }

        private void Abort(string taskId, string error)
        {
            if (!transfers.TryGetValue(taskId, out TransferState? state)) return;
            lock (state.Gate)
            {
                if (!transfers.TryGetValue(taskId, out TransferState? current) || !ReferenceEquals(current, state)) return;
                if (!transfers.TryRemove(taskId, out _)) return;
                state.Stream?.Dispose();
                state.Stream = null;
            }
            state.CancellationRegistration.Unregister();
            try { if (state.FileCreated && File.Exists(state.Job.path)) File.Delete(state.Job.path); } catch { }
            ReportError(taskId, error);
            messageManager.CompleteJob(taskId);
        }

        private void ReportError(string taskId, string error) =>
            messageManager.AddTaskResponse(new TaskResponse { status = "error", completed = true, task_id = taskId, user_output = error }.ToJson());

        private static string GetStatusBar(int chunkNumber, int totalChunks)
        {
            const int width = 50;
            double progress = (double)chunkNumber / totalChunks;
            int filled = (int)(progress * width);
            return $"[{new string('#', filled)}{new string('-', width - filled)}] {progress:P0}";
        }
    }
}
