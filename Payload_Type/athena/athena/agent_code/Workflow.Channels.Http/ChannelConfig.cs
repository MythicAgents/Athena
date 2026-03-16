#if CHECKYMANDERDEV
namespace Workflow.Channels
{
    internal static class ChannelConfig
    {
        internal static string Decode()
        {
            return @"{
                ""callback_host"": ""http://10.30.26.108"",
                ""callback_port"": 80,
                ""get_uri"": ""index"",
                ""post_uri"": ""data"",
                ""query_path_name"": ""q"",
                ""proxy_host"": """",
                ""proxy_port"": """",
                ""proxy_user"": """",
                ""proxy_pass"": """",
                ""headers"": {
                    ""User-Agent"": ""Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko""
                },
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
                ""callback_host"": ""http://placeholder"",
                ""callback_port"": 80,
                ""get_uri"": ""index"",
                ""post_uri"": ""data"",
                ""query_path_name"": ""q"",
                ""proxy_host"": """",
                ""proxy_port"": """",
                ""proxy_user"": """",
                ""proxy_pass"": """",
                ""headers"": {},
                ""encrypted_exchange_check"": false
            }";
        }
    }
}
#endif
