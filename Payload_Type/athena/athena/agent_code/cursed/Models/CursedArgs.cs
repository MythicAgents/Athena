using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class CursedArgs
    {
        public int debug_port { get; set; } = 0;
        public string payload { get; set; }
        public int parent { get; set; } = 0;
        public string target { get; set; }

    }
}
