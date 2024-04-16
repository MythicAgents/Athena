using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class ProcessTaskResponse : TaskResponse
    {
        public List<ServerProcessInfo> processes { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, ProcessResponseJsonContext.Default.ProcessTaskResponse);
        }
    }
    [JsonSerializable(typeof(ProcessTaskResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class ProcessResponseJsonContext : JsonSerializerContext
    {
    }

    public class ServerProcessInfo
    {
        public int process_id { get; set; }
        public string architecture { get; set; }
        public string name { get; set; }
        public string user { get; set; }
        public string bin_path { get; set; }
        public int parent_process_id { get; set; }
        public string command_line { get; set; }
        public long start_time { get; set; }
        public string description { get; set; }
        public string signer { get; set; }
        public bool update_deleted = true;
    }
}
