using System.Text.Json.Serialization;

namespace Agent.Config
{
    [JsonSerializable(typeof(AgentConfigOptions))]
    internal partial class AgentConfigOptionsJsonContext : JsonSerializerContext { }

    internal class AgentConfigOptions
    {
        [JsonPropertyName("uuid")] public string Uuid { get; set; } = "";
        [JsonPropertyName("plugin_contract_fingerprint_required")] public bool PluginContractFingerprintRequired { get; set; }
        [JsonPropertyName("psk")] public string Psk { get; set; } = "";
        [JsonPropertyName("callback_interval"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] public int CallbackInterval { get; set; } = 60;
        [JsonPropertyName("callback_jitter"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] public int CallbackJitter { get; set; } = 10;
        [JsonPropertyName("killdate")] public string KillDate { get; set; } = "";
    }
}
