using System;
using System.Text.Json.Serialization;

namespace Athena.Models.Mythic.Response
{
    [Serializable]
    public class DelegateMessage
    {
        public string message { get; set; }
        public string c2_profile { get; set; }
        public string uuid { get; set; }
        public bool final { get; set; }
    }
    [JsonSerializable(typeof(DelegateMessage))]
    public partial class DelegateMessageJsonContext : JsonSerializerContext
    {
    }
}
