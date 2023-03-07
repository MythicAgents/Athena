using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Athena.Plugins;

namespace Athena.Models
{
    public class DownloadResponse : ResponseResult
    {
        public DownloadResponseData download { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, DownloadResponseJsonContext.Default.DownloadResponse);
        }
    }
    [JsonSerializable(typeof(DownloadResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class DownloadResponseJsonContext : JsonSerializerContext
    {
    }

    public class DownloadResponseData
    {
        public int total_chunks { get; set; }
        public string full_path { get; set; }
        public int chunk_num { get; set; }
        public bool is_screenshot { get; set; } = false;
        public string file_id { get; set; }
        public string host { get; set; } = "";
        public string chunk_data { get; set; }
    }
}
