using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Plugins
{
    public class ProcessResponseResult : ResponseResult
    {
        public List<MythicProcessInfo> processes { get; set; }
    }

    public class MythicProcessInfo
    {
        public int process_id { get; set; }
        public string architecture { get; set; }
        public string name { get; set; }
        public string user { get; set; }
        public string bin_path { get; set; }
        public int parent_process_id { get; set; }
        public string command_line { get; set; }
        public string start_time { get; set; }
        public string description { get; set; }
        public string signer { get; set; }
    }
}
