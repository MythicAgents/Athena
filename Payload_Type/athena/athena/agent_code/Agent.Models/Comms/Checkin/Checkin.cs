using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class Checkin
    {
        public string action { get; set; }
        public List<string> ips { get; set; }
        public string os { get; set; }
        public string user { get; set; }
        public string host { get; set; }
        public int pid { get; set; }
        public string uuid { get; set; }
        public string architecture { get; set; }
        public string domain { get; set; }
        public int integrity_level { get; set; }
        public string external_ip { get; set; } = String.Empty;
        public string encryption_key { get; set; } = String.Empty;
        public string decryption_key { get; set; } = String.Empty;
        public string process_name { get; set; } = String.Empty;
    }
    
    [JsonSerializable(typeof(Checkin))]
    public partial class CheckinJsonContext : JsonSerializerContext
    {
    }
}
