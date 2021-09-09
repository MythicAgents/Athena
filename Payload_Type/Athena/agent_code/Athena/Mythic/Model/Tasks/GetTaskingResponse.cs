using Athena.Mythic.Model.Response;
using System.Collections.Generic;

namespace Athena.Mythic.Model
{
    public class GetTaskingResponse
    {
        public string action;
        public List<MythicTask> tasks;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;

        //public Dictionary<string, string> delegates;
    }
}
