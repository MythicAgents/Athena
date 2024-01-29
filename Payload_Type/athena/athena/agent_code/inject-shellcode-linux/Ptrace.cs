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
        public const int PTRACE_POKETEXT = 1;
        public const int PTRACE_GETREGS = 12;
        public const int PTRACE_SETREGS = 13;
        public const int PTRACE_CONT = 7;

        [StructLayout(LayoutKind.Sequential)]
        public struct UserRegs
        {
            public ulong r15, r14, r13, r12, rbp, rbx, r11, r10, r9, r8, rax, rcx, rdx, rsi, rdi, orig_rax, rip, cs, eflags, rsp, ss, fs_base, gs_base, ds, es, fs, gs;
        }

        [DllImport("libc", SetLastError = true)]
        public static extern int ptrace(int request, long pid, IntPtr addr, IntPtr data);


        [DllImport("libc", SetLastError = true)]
        public static extern int waitpid(long pid, out int status, int options);

        public static int PtraceAttach(long pid)
        {
            return ptrace(PTRACE_ATTACH, pid, IntPtr.Zero, IntPtr.Zero);
        }

        public static int PtracePokeText(long pid, long addr, ulong data)
        {
            return ptrace(PTRACE_POKETEXT, pid, (IntPtr)addr, (IntPtr)data);
        }

        public static int PtraceGetRegs(long pid, out UserRegs regs)
        {
            IntPtr regsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UserRegs)));
            try
            {
                int result = ptrace(PTRACE_GETREGS, pid, IntPtr.Zero, regsPtr);
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
                return ptrace(PTRACE_SETREGS, pid, IntPtr.Zero, regsPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(regsPtr);
            }
        }
        public static int PtraceCont(long pid, IntPtr addr)
        {
            return ptrace(PTRACE_CONT, pid, IntPtr.Zero, addr);
        }

        public static void Wait(int? status)
        {
            int stat;
            waitpid(-1, out stat, 0);
            if (status != null)
            {
                status = stat;
            }
        }
    }
}
