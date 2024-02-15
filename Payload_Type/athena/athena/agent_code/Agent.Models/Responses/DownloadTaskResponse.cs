using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class DownloadTaskResponse : TaskResponse
    {
        public DownloadTaskResponseData download { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, DownloadTaskResponseJsonContext.Default.DownloadTaskResponse);
        }
    }
    [JsonSerializable(typeof(DownloadTaskResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class DownloadTaskResponseJsonContext : JsonSerializerContext
    {
    }

    public class DownloadTaskResponseData
    {
        public int total_chunks { get; set; }
        public string full_path { get; set; }
        public string filename { get; set; }
        public int chunk_num { get; set; }
        public bool is_screenshot { get; set; } = false;
        public string file_id { get; set; }
        public string host { get; set; } = "";
        public string chunk_data { get; set; }
    }
}
