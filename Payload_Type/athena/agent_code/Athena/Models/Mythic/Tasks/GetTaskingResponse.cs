using Athena.Models.Mythic.Response;
using System.Collections.Generic;

namespace Athena.Models.Mythic.Tasks { 

    public class GetTaskingResponse
    {
        public string action;
        public List<MythicTask> tasks;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;
    }
}
