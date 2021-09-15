using Athena.Mythic.Model.Response;
using System.Collections.Generic;

namespace Athena.Mythic.Model
{
    public class GetTasking
    {
        public string action;
        public int tasking_size;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;
    }
}
