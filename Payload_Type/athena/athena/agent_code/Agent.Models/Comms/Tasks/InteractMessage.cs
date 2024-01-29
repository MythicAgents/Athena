using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Agent.Models
{
    [Serializable]
    public class InteractMessage
    {
        public string task_id { get; set; }
        public string data { get; set; }
        public InteractiveMessageType message_type { get; set; }
    }
    [JsonSerializable(typeof(InteractMessage))]
    public partial class InteractResponseJsonContext : JsonSerializerContext
    {
    }
}
