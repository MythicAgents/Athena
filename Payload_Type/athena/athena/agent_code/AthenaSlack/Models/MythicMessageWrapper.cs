using System.Text.Json.Serialization;

namespace Athena.Profiles.Slack
{
    public class MythicMessageWrapper
    {
        public string message { get; set; } = String.Empty;
        public string sender_id { get; set; } //Who sent the message
        public bool to_server { get; set; }
        public int id { get; set; }
        public bool final { get; set; }
    }
    [JsonSerializable(typeof(MythicMessageWrapper))]
    public partial class MythicMessageWrapperJsonContext : JsonSerializerContext
    {
    }
}
