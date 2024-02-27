using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public interface ITaskResponse {
        public string task_id { get; set; }
        public string user_output { get; set; }
        public string status { get; set; }
        public bool completed { get; set; }
        public Dictionary<string, string> process_response { get; set; }
        public string file_id { get; set; }
        public string ToJson();
    
    }


    public class TaskResponse : ITaskResponse
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
            return JsonSerializer.Serialize(this, TaskResponseJsonContext.Default.TaskResponse);
        }
    }
    [JsonSerializable(typeof(TaskResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class TaskResponseJsonContext : JsonSerializerContext
    {
    }
}
