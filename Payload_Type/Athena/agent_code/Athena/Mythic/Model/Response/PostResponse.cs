using System.Collections.Generic;

namespace Athena.Mythic.Model.Response
{
    //If you post a response to the same taskuuid it will keep appending data to the task (good for long running programs)
    public class PostResponse
    {
        public string action;
        public List<ResponseResult> responses;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;
    }
}
