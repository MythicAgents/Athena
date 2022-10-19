using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace inject_assembly.Techniques
{
    public interface ITechnique
    {
        public abstract bool Inject(byte[] shellcode, IntPtr hTarget);
    }
}
