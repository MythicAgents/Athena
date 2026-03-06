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
    }
}
