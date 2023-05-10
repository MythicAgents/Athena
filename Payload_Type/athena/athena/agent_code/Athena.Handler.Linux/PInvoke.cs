using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Utilities
{
    public static class PInvoke
    {
        [DllImport("libc")]
        public static extern uint geteuid();
    }
}
