using Athena.Mythic.Model.Response;
using System.Collections.Generic;

namespace Athena.Mythic.Hooks
{
    public class PostResponseResponse
    {
        public string action;
        public List<ResponseResult> responses;
        //Dictionary<string,string> delegates;
    }
}
