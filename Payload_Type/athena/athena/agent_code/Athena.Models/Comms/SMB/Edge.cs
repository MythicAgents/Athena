using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Comms.SMB
{
    public class Edge
    {
        public string source { get; set; }
        public string destination { get; set; }
        public string metadata { get; set; }
        public string action { get; set; }
        public string c2_profile { get; set; }
    }
}
