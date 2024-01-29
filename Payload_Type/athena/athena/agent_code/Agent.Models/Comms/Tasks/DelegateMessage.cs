using System.Text.Json.Serialization;

namespace Agent.Models
{
    [Serializable]
    public class DelegateMessage
    {
        public string message { get; set; }
        public string c2_profile { get; set; }
        public string uuid { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string mythic_uuid { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string new_uuid { get; set; }


    }
    [JsonSerializable(typeof(DelegateMessage))]
    public partial class DelegateMessageJsonContext : JsonSerializerContext
    {
    }
}
