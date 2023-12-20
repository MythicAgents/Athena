using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class CreateToken
    {
        public string username { get; set; }
        public string password { get; set; }
        public string domain { get; set; }
        public string name { get; set; }
        public bool netOnly { get; set; }
    }
    [JsonSerializable(typeof(CreateToken))]
    public partial class CreateTokenJsonContext : JsonSerializerContext
    {
    }
}
