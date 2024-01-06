using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class ShellcodeArgs
    {
        //public bool output { get; set; }
        public string asm { get; set; }

        public bool Validate()
        {
            return asm.Length > 0;
        }
    }
}
