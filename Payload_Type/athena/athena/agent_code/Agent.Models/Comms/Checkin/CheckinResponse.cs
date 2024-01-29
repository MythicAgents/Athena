using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class CheckinResponse
    {
        public string status { get; set; }
        public string id { get; set; }
        public string action { get; set; }
        public string encryption_key { get; set; }
        public string decryption_key { get; set; }
        public string process_name { get; set; }
    }
    [JsonSerializable(typeof(CheckinResponse))]
    public partial class CheckinResponseJsonContext : JsonSerializerContext
    {
    }
}
