using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public static class PTrace
    {
        public const int PTRACE_ATTACH = 16;
        public const int PTRACE_PEEKTEXT = 1;
        public const int PTRACE_POKETEXT = 4;
        public const int PTRACE_GETREGS = 12;
        public const int PTRACE_SETREGS = 13;
        public const int PTRACE_CONT = 7;
        public const int PTRACE_DETACH = 17;

        [StructLayout(LayoutKind.Sequential)]
        public struct UserRegs
        {
            public ulong r15, r14, r13, r12, rbp, rbx, r11, r10, r9, r8, rax, rcx, rdx, rsi, rdi, orig_rax, rip, cs, eflags, rsp, ss, fs_base, gs_base, ds, es, fs, gs;
        }

        [DllImport("libc", SetLastError = true)]
        public static extern long ptrace(int request, long pid, IntPtr addr, IntPtr data);


        [DllImport("libc", SetLastError = true)]
        public static extern int waitpid(long pid, out int status, int options);

        public static int PtraceAttach(long pid)
        {
            return (int)ptrace(PTRACE_ATTACH, pid, IntPtr.Zero, IntPtr.Zero);
        }

        public static int PtracePokeText(long pid, long addr, ulong data)
        {
            return (int)ptrace(PTRACE_POKETEXT, pid, (IntPtr)addr, (IntPtr)data);
        }

        public static int PtracePeekText(long pid, long addr, out ulong data)
        {
            Marshal.SetLastPInvokeError(0);
            long result = ptrace(PTRACE_PEEKTEXT, pid, (IntPtr)addr, IntPtr.Zero);
            int error = Marshal.GetLastPInvokeError();
            data = unchecked((ulong)result);
            return result == -1 && error != 0 ? -1 : 0;
        }

        public static int PtraceGetRegs(long pid, out UserRegs regs)
        {
            IntPtr regsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UserRegs)));
            try
            {
                int result = (int)ptrace(PTRACE_GETREGS, pid, IntPtr.Zero, regsPtr);
                regs = result == 0 ? Marshal.PtrToStructure<UserRegs>(regsPtr) : default;
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(regsPtr);
            }
        }

        public static int PtraceSetRegs(long pid, UserRegs regs)
        {
            IntPtr regsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(regs));
            try
            {
                Marshal.StructureToPtr(regs, regsPtr, false);
                return (int)ptrace(PTRACE_SETREGS, pid, IntPtr.Zero, regsPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(regsPtr);
            }
        }
        public static int PtraceCont(long pid, IntPtr addr)
        {
            return (int)ptrace(PTRACE_CONT, pid, IntPtr.Zero, addr);
        }

        public static int PtraceDetach(long pid)
        {
            return (int)ptrace(PTRACE_DETACH, pid, IntPtr.Zero, IntPtr.Zero);
        }

        public static int Wait(long pid)
        {
            return waitpid(pid, out _, 0);
        }
    }
}
