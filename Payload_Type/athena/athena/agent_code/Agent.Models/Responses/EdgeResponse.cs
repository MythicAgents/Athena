using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class EdgeResponse : TaskResponse
    {
        public List<Edge> edges { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, EdgeResponseJsonContext.Default.EdgeResponse);
        }
    }
    [JsonSerializable(typeof(EdgeResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(Edge))]
    public partial class EdgeResponseJsonContext : JsonSerializerContext
    {
    }
}
