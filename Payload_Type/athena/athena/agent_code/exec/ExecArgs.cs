using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    internal class ExecArgs
    {
        public int parent { get; set; } = 0;
        public string commandline { get; set; } = "";
        public bool blockDlls { get; set; } = false;
        public bool output { get; set; } = false;
        public bool spoofParent { get; set; } = false;
    }
}
