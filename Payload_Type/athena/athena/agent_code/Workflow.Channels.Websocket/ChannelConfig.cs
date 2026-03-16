#if CHECKYMANDERDEV
namespace Workflow.Channels.Websocket
{
    internal static class ChannelConfig
    {
        internal static string Decode()
        {
            return @"{
                ""callback_host"": ""wss://localhost"",
                ""callback_port"": 7443,
                ""ENDPOINT_REPLACE"": ""socket"",
                ""USER_AGENT"": ""Mozilla/5.0"",
                ""domain_front"": """",
                ""encrypted_exchange_check"": true
            }";
        }
    }
}
#else
namespace Workflow.Channels.Websocket
{
    internal static class ChannelConfig
    {
        internal static string Decode()
        {
            return @"{
                ""callback_host"": ""wss://placeholder"",
                ""callback_port"": 443,
                ""ENDPOINT_REPLACE"": ""socket"",
                ""USER_AGENT"": ""Mozilla/5.0"",
                ""domain_front"": """",
                ""encrypted_exchange_check"": false
            }";
        }
    }
}
#endif
