using System.Collections.Generic;

namespace Athena.Models.Mythic.Response
{
    public class PostResponseResponse
    {
        public string action;
        public List<ResponseResult> responses;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;
    }
    public class PostUploadResponseResponse
    {
        public string action;
        public List<UploadResponseResponse> responses;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;
    }
}
