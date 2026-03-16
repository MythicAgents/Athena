#if CHECKYMANDERDEV
namespace Workflow.Channels.Smb
{
    internal static class ChannelConfig
    {
        internal static string Decode()
        {
            return @"{
                ""pipename"": ""scottie_pipe"",
                ""encrypted_exchange_check"": true
            }";
        }
    }
}
#else
namespace Workflow.Channels.Smb
{
    internal static class ChannelConfig
    {
        internal static string Decode()
        {
            return @"{
                ""pipename"": ""placeholder"",
                ""encrypted_exchange_check"": false
            }";
        }
    }
}
#endif
