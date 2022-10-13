using Athena.Models.Mythic.Checkin;
using Athena.Models.Mythic.Response;
using Athena.Plugins;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Athena.Models.Mythic.Tasks
{
    public class GetTasking
    {
        public string action { get; set; }
        public int tasking_size { get; set; }
        public List<SocksMessage> socks { get; set; }
        public List<DelegateMessage> delegates { get; set; }
        public List<object> responses { get; set; }
    }
    [JsonSerializable(typeof(GetTasking))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(bool))]
    public partial class GetTaskingJsonContext : JsonSerializerContext
    {
    }
}
