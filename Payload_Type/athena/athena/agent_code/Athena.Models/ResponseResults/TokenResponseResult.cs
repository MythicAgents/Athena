using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Athena.Models
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
        public int TokenId { get; set; }
        public string description { get; set; }
        public string user { get; set; }
        public long Handle { get; set; }
    }
    public class CallbackToken
    {
        public string action { get; set; }
        public string host { get; set; }
        public int TokenId { get; set; }
    }
}
