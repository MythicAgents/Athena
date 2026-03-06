using System.Text.Json.Serialization;

namespace Workflow.Config
{
    [JsonSerializable(typeof(ServiceConfigOptions))]
    internal partial class ServiceConfigOptionsJsonContext : JsonSerializerContext { }

    internal class ServiceConfigOptions
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = "";

        [JsonPropertyName("psk")]
        public string Psk { get; set; } = "";

        [JsonPropertyName("callback_interval")]
        public int CallbackInterval { get; set; } = 60;

        [JsonPropertyName("callback_jitter")]
        public int CallbackJitter { get; set; } = 10;

        [JsonPropertyName("killdate")]
        public string KillDate { get; set; } = "";
    }
}
