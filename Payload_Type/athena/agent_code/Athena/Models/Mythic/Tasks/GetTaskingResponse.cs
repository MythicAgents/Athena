using Athena.Models.Mythic.Response;
using System.Collections.Generic;

namespace Athena.Models.Mythic.Tasks { 

    public class GetTaskingResponse
    {
        public string action;
        public List<MythicTask> tasks;
        public List<SocksMessage> socks;
        public List<DelegateMessage> delegates;
        public List<MythicResponseResult> responses;
    }

    public class MythicResponseResult
    {
        public string task_id;
        public string status;
        public string file_id;
        public int total_chunks;
        public int chunk_num;
        public string chunk_data;
    }
}
