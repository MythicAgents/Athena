using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reg
{
    public class RegArgs
    {
        public string hostName { get; set; }
        public string keyName { get; set; }
        public string keyPath { get; set; }
        public string keyValue { get; set; }
        public string keyType { get; set; } //Unused yet
        public string action { get; set; }

    }
}
