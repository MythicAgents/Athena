using Athena.Models.Comms.SMB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Athena.Models.Responses
{
    public class KeystrokesResponseResult : ResponseResult
    {
        public List<Keylogs> keylogs { get; set; }

        public void Prepare()
        {
            foreach(var keylog in keylogs)
            {
                keylog.keystrokes = keylog.builder.ToString();
            }
        }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, KeystrokesResponseJsonContext.Default.KeystrokesResponseResult);
        }
    }
    public class Keylogs
    {
        public string user { get; set; }
        public string window_title { get; set; }
        public string keystrokes { get; set; }
        [JsonIgnore]
        public StringBuilder builder { get; set; }
    }
    [JsonSerializable(typeof(KeystrokesResponseResult))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(Keylogs))]
    public partial class KeystrokesResponseJsonContext : JsonSerializerContext
    {
    }
}
