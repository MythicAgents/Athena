#if CHECKYMANDERDEV
namespace Workflow.Channels
{
    internal static class ChannelConfig
    {
        internal static string Decode()
        {
            return @"{
                ""personal_access_token"": ""ghp_dev_token_here"",
                ""github_username"": ""testuser"",
                ""github_repo"": ""testrepo"",
                ""server_issue_number"": 1,
                ""client_issue_number"": 2,
                ""encrypted_exchange_check"": true
            }";
        }
    }
}
#endif
