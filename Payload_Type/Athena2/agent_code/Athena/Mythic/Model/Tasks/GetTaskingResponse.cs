using Athena.Mythic.Model;
using System.Collections.Generic;

namespace Athena.Mythic.Hooks
{
    public class GetTaskingResponse
    {
        public string action;
        public List<MythicTask> tasks;

        //public Dictionary<string, string> delegates;
    }
}
