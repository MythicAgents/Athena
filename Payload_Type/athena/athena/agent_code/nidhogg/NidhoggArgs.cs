using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace nidhogg
{
    public class NidhoggArgs
    {
        public string command { get; set; }
        public string script { get; set; } = String.Empty;
        public string path { get; set; } = String.Empty;
        public string value { get; set; } = String.Empty;
        public uint id { get; set; } = 0;

    }
}
