#if CHECKYMANDERDEV
namespace Workflow.Channels
{
    internal static class ChannelConfig
    {
        internal static string Decode()
        {
            return @"{
                ""discord_token"": ""your-dev-token-here"",
                ""bot_channel"": ""123456789"",
                ""encrypted_exchange_check"": true
            }";
        }
    }
}
#else
namespace Workflow.Channels
{
    internal static class ChannelConfig
    {
        internal static string Decode()
        {
            return @"{
                ""discord_token"": ""placeholder"",
                ""bot_channel"": ""0"",
                ""encrypted_exchange_check"": false
            }";
        }
    }
}
#endif
