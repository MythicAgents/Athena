using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Agent.Managers
{
    public static class Native
    {
        [DllImport("libc")]
        public static extern uint geteuid();
    }
}
