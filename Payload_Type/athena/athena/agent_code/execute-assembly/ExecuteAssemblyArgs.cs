using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class ExecuteAssemblyArgs
    {
        public string asm { get; set; } = string.Empty;
        public string arguments { get; set; } = string.Empty;

        public bool Validate()
        {
            if (asm.Length == 0) return false;
            return true;
        }
    }
}
