using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginBase
{
    public class TokenResponseResult : ResponseResult
    {
        //public List<Token> tokens { get; set; }
        public List<CallbackToken> callbacktokens { get; set; }
    }

    public class Token
    {
        public int TokenId { get; set; }
        public string description { get; set; }
        public string user { get; set; }
        public SafeAccessTokenHandle Handle { get; set; }
    }
    public class CallbackToken
    {
        public string action { get; set; }
        public string host { get; set; }
        public int TokenId { get; set; }
    }
}
