using Athena.Models.Comms.SMB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Athena.Models.Responses
{
    public class EdgeResponseResult : ResponseResult
    {
        public List<Edge> edges { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, EdgeResponseJsonContext.Default.EdgeResponseResult);
        }
    }
    [JsonSerializable(typeof(EdgeResponseResult))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(Edge))]
    public partial class EdgeResponseJsonContext : JsonSerializerContext
    {
    }
}
