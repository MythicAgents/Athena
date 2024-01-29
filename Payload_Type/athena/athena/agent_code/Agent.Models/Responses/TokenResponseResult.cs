using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class TokenResponseResult : ResponseResult
    {
        public List<Token> tokens { get; set; }
        public List<CallbackToken> callback_tokens { get; set; }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, TokenResponseJsonContext.Default.TokenResponseResult);
        }
    }
    [JsonSerializable(typeof(TokenResponseResult))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    public partial class TokenResponseJsonContext : JsonSerializerContext
    {
    }

    public class Token
    {
        public string action { get; set; }
        public int token_id { get; set; }
        public string description { get; set; }
        public string user { get; set; }
        public long Handle { get; set; }
        public int process_id { get; set; }
    }
    public class CallbackToken
    {
        public string action { get; set; }
        public string host { get; set; }
        public int token_id { get; set; }
    }
}
