using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class SshArgs
    {
        public string hostname { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string keypath { get; set; } 
    }
}
