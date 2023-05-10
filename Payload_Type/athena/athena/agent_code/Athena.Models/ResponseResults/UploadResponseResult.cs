using System.Text.Json;
using System.Text.Json.Serialization;

namespace Athena.Models
{
    public class UploadResponse : ResponseResult
    {
        public UploadResponseData upload { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, UploadResponseJsonContext.Default.UploadResponse);
        }
    }
    [JsonSerializable(typeof(UploadResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class UploadResponseJsonContext : JsonSerializerContext
    {
    }

    public class UploadResponseData
    {
        public int chunk_size { get; set; }
        public int chunk_num { get; set; }
        public string file_id { get; set; }
        public string full_path { get; set; }
    }
}
