using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class UploadTaskResponse : TaskResponse
    {
        public UploadTaskResponseData upload { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, UploadTaskResponseJsonContext.Default.UploadTaskResponse);
        }
    }
    [JsonSerializable(typeof(UploadTaskResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class UploadTaskResponseJsonContext : JsonSerializerContext
    {
    }

    public class UploadTaskResponseData
    {
        public int chunk_size { get; set; }
        public int chunk_num { get; set; }
        public string file_id { get; set; }
        public string full_path { get; set; }
    }
}
