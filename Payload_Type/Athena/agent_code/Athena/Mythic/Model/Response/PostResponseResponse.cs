using Athena.Mythic.Model.Response;
using System.Collections.Generic;

namespace Athena.Mythic.Model
{
    public class PostResponseResponse
    {
        public string action;
        public List<ResponseResult> responses;
        //Dictionary<string,string> delegates;
    }
    public class PostUploadResponseResponse
    {
        public string action;
        public List<UploadResponseResponse> responses;
    }
}
