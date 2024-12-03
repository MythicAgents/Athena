using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace port_bender
{
    public class PortBenderArgs
    {
        public int port { get; set; } = 0;
        public string destination { get; set; } = string.Empty;

        public bool Validate()
        {
            return (this.port > 0 && !string.IsNullOrEmpty(this.destination));
        }
    }
}