using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace execute_module
{
    public class AthenaModule
    {
        public string name { get; set; } = string.Empty;
        public string entrypoint { get; set; } = string.Empty;
        public List<byte> fContent = new List<byte>();
        public Assembly asm { get; set; }
    }
}
