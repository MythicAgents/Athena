using Athena.Mythic.Model.Response;
using System.Collections.Generic;

namespace Athena.Mythic.Model
{
    public class PostResponseResponse
    {
        public string action;
        public List<ResponseResult> responses;
        public List<DelegateMessage> delegates;
    }
    public class PostUploadResponseResponse
    {
        public string action;
        public List<UploadResponseResponse> responses;
        public List<DelegateMessage> delegates;
    }
}
