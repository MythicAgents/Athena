using Athena.Models.Mythic.Response;
using System.Collections.Generic;

namespace Athena.Models.Mythic.Tasks
{
    public class GetTasking
    {
        public string action;
        public int tasking_size;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;
        public List<ResponseResult> responses;
    }
}
