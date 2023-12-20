using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class LoadCommandResponseResult : ResponseResult
    {
        public List<CommandsResponse> commands { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, LoadCommandResponseJsonContext.Default.LoadCommandResponseResult);
        }
    }
    [JsonSerializable(typeof(LoadCommandResponseResult))]
    [JsonSerializable(typeof(string))]
    public partial class LoadCommandResponseJsonContext : JsonSerializerContext
    {
    }
    public class CommandsResponse
    {
        public string action { get; set; }
        public string cmd { get; set; }
    }
}
