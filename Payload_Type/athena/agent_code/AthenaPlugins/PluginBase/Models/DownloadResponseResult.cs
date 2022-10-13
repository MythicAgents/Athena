using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Athena.Plugins;

namespace Athena.Plugins
{
    public class DownloadResponse : ResponseResult
    {
        public int total_chunks { get; set; }
        public string full_path { get; set; }
        public int chunk_num { get; set; }
        public string chunk_data { get; set; }
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
}
