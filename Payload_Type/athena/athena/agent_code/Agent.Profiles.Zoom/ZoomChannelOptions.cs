using System.Text.Json.Serialization;

namespace Agent.Profiles
{
    [JsonSerializable(typeof(ZoomChannelOptions))]
    internal partial class ZoomChannelOptionsJsonContext : JsonSerializerContext { }

    internal class ZoomChannelOptions
    {
        [JsonPropertyName("account_id")] public string AccountId { get; set; } = "";
        [JsonPropertyName("client_id")] public string ClientId { get; set; } = "";
        [JsonPropertyName("client_secret")] public string ClientSecret { get; set; } = "";
        [JsonPropertyName("user_id")] public string UserId { get; set; } = "me";
        [JsonPropertyName("channel_id")] public string ChannelId { get; set; } = "";
        [JsonPropertyName("api_base")] public string ApiBase { get; set; } = "https://api.zoom.us/v2";
        [JsonPropertyName("oauth_base")] public string OAuthBase { get; set; } = "https://zoom.us/oauth";
        [JsonPropertyName("encrypted_exchange_check")] public bool EncryptedExchangeCheck { get; set; }
    }
}
