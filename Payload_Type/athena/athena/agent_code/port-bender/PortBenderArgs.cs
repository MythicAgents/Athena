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
        public string destination { get; set; }

        public bool Validate()
        {
            return (this.port > 0 && !String.IsNullOrEmpty(this.destination));
        }
    }
}