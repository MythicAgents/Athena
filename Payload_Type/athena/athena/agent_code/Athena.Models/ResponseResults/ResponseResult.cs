using System.Text.Json;
using System.Text.Json.Serialization;

namespace Athena.Models
{
    public interface IResponseResult {
        public string task_id { get; set; }
        public string user_output { get; set; }
        public string status { get; set; }
        public bool completed { get; set; }
        public Dictionary<string, string> process_response { get; set; }
        public string file_id { get; set; }
        public string ToJson();
    
    }


    public class ResponseResult : IResponseResult
    {
        public string task_id { get; set; }
        public string user_output { get; set; }
        public string status { get; set; }
        public Dictionary<string, string> process_response { get; set; }
        public bool completed { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string file_id { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, ResponseResultJsonContext.Default.ResponseResult);
        }
    }
    [JsonSerializable(typeof(ResponseResult))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class ResponseResultJsonContext : JsonSerializerContext
    {
    }
}
