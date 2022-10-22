using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Athena.Plugins
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
