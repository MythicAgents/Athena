using System.Text.Json.Serialization;

namespace Workflow.Channels
{
    [JsonSerializable(typeof(HttpChannelOptions))]
    internal partial class HttpChannelOptionsJsonContext : JsonSerializerContext { }

    internal class HttpChannelOptions
    {
        [JsonPropertyName("callback_host")]
        public string CallbackHost { get; set; } = "";

        [JsonPropertyName("callback_port")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int CallbackPort { get; set; }

        [JsonPropertyName("get_uri")]
        public string GetUri { get; set; } = "";

        [JsonPropertyName("post_uri")]
        public string PostUri { get; set; } = "";

        [JsonPropertyName("query_path_name")]
        public string QueryPathName { get; set; } = "";

        [JsonPropertyName("proxy_host")]
        public string ProxyHost { get; set; } = "";

        [JsonPropertyName("proxy_port")]
        public string ProxyPort { get; set; } = "";

        [JsonPropertyName("proxy_user")]
        public string ProxyUser { get; set; } = "";

        [JsonPropertyName("proxy_pass")]
        public string ProxyPass { get; set; } = "";

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new();

        [JsonPropertyName("encrypted_exchange_check")]
        public bool EncryptedExchangeCheck { get; set; }
    }
}
