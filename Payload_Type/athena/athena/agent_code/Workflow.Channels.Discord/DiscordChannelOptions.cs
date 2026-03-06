using System.Text.Json.Serialization;

namespace Workflow.Channels
{
    [JsonSerializable(typeof(DiscordChannelOptions))]
    internal partial class DiscordChannelOptionsJsonContext : JsonSerializerContext { }

    internal class DiscordChannelOptions
    {
        [JsonPropertyName("discord_token")]
        public string DiscordToken { get; set; } = "";

        [JsonPropertyName("bot_channel")]
        public string BotChannel { get; set; } = "";

        [JsonPropertyName("encrypted_exchange_check")]
        public bool EncryptedExchangeCheck { get; set; }
    }
}
