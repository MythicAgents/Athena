using System.Collections.Generic;

namespace Athena.Models.Mythic.Response
{
    public class PostResponse
    {
        public string action;
        public List<ResponseResult> responses;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;
    }
}
