using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class ExecuteAssemblyArgs
    {
        public string asm { get; set; }
        public string arguments { get; set; }

        public bool Validate()
        {
            if (asm.Length == 0) return false;
            return true;
        }
    }
}
