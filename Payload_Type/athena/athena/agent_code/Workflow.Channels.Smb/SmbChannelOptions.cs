using System.Text.Json.Serialization;

namespace Workflow.Channels.Smb
{
    [JsonSerializable(typeof(SmbChannelOptions))]
    internal partial class SmbChannelOptionsJsonContext : JsonSerializerContext { }

    internal class SmbChannelOptions
    {
        [JsonPropertyName("pipename")]
        public string PipeName { get; set; } = "";

        [JsonPropertyName("encrypted_exchange_check")]
        public bool EncryptedExchangeCheck { get; set; }

        [JsonPropertyName("connection_timeout_seconds")]
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        [JsonPropertyName("checkin_timeout_seconds")]
        public int CheckinTimeoutSeconds { get; set; } = 60;

        [JsonPropertyName("message_ack_timeout_seconds")]
        public int MessageAckTimeoutSeconds { get; set; } = 15;

        [JsonPropertyName("chunk_size")]
        public int ChunkSize { get; set; } = 32768;
    }
}
