using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Athena.Models
{
    public class ProcessResponseResult : ResponseResult
    {
        public List<MythicProcessInfo> processes { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, ProcessResponseJsonContext.Default.ProcessResponseResult);
        }
    }
    [JsonSerializable(typeof(ProcessResponseResult))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class ProcessResponseJsonContext : JsonSerializerContext
    {
    }

    public class MythicProcessInfo
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
    }
}
