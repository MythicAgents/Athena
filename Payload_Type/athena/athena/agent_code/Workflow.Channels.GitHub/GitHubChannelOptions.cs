using System.Text.Json.Serialization;

namespace Workflow.Channels
{
    [JsonSerializable(typeof(GitHubChannelOptions))]
    internal partial class GitHubChannelOptionsJsonContext : JsonSerializerContext { }

    internal class GitHubChannelOptions
    {
        [JsonPropertyName("personal_access_token")]
        public string PersonalAccessToken { get; set; } = "";

        [JsonPropertyName("github_username")]
        public string GithubUsername { get; set; } = "";

        [JsonPropertyName("github_repo")]
        public string GithubRepo { get; set; } = "";

        [JsonPropertyName("server_issue_number")]
        public int ServerIssueNumber { get; set; }

        [JsonPropertyName("client_issue_number")]
        public int ClientIssueNumber { get; set; }

        [JsonPropertyName("encrypted_exchange_check")]
        public bool EncryptedExchangeCheck { get; set; }
    }
}
