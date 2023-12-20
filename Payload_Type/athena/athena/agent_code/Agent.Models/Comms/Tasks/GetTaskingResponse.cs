using System.Text.Json.Serialization;

namespace Agent.Models {

    public class GetTaskingResponse
    {
        public string action { get; set; }
        public List<ServerTask> tasks { get; set; }
        public List<ServerDatagram> socks { get; set; }
        public List<ServerDatagram> rpfwd { get; set; }
        public List<DelegateMessage> delegates { get; set; }
        public List<ServerResponseResult> responses { get; set; }
    }
    [Serializable]
    public class ServerResponseResult
    {
        public string task_id { get; set; }
        public string status { get; set; }
        public string file_id { get; set; }
        public int total_chunks { get; set; }
        public int chunk_num { get; set; }
        public string chunk_data { get; set; }
    }

    [JsonSerializable(typeof(GetTaskingResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(bool))]
    public partial class GetTaskingResponseJsonContext : JsonSerializerContext
    {
    }
}
