using System.Text.Json.Serialization;

namespace Workflow.Channels.Websocket
{
    [JsonSerializable(typeof(WebsocketChannelOptions))]
    internal partial class WebsocketChannelOptionsJsonContext : JsonSerializerContext { }

    internal class WebsocketChannelOptions
    {
        [JsonPropertyName("callback_host")]
        public string CallbackHost { get; set; } = "";

        [JsonPropertyName("callback_port")]
        public int CallbackPort { get; set; }

        [JsonPropertyName("ENDPOINT_REPLACE")]
        public string Endpoint { get; set; } = "";

        [JsonPropertyName("USER_AGENT")]
        public string UserAgent { get; set; } = "";

        [JsonPropertyName("domain_front")]
        public string DomainFront { get; set; } = "";

        [JsonPropertyName("encrypted_exchange_check")]
        public bool EncryptedExchangeCheck { get; set; }
    }
}
