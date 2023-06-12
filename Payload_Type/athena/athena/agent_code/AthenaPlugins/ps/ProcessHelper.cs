using Athena.Models.Responses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ps
{
    public class ProcessHelper
    {
        private const int ERROR_NO_MORE_FILES = 18;

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, [Out] char[] lpExeName, ref int lpdwSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public int th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public int th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const int MAX_PATH = 260;

        public static List<MythicProcessInfo> GetProcessesWithParent()
        {
            IntPtr snapshotHandle = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshotHandle == IntPtr.Zero)
            {
                return new List<MythicProcessInfo>();
            }

            PROCESSENTRY32 processEntry = new PROCESSENTRY32();
            processEntry.dwSize = (uint)Marshal.SizeOf(processEntry);

            if (!Process32First(snapshotHandle, ref processEntry))
            {
                CloseHandle(snapshotHandle);
                return new List<MythicProcessInfo>();
            }

            List<MythicProcessInfo> procs = new List<MythicProcessInfo>();
            Process[] managedProcs = Process.GetProcesses(); //Get all processes for later use so we're not doing a bunch of OpenProcesses
            do
            {
                Process proc;
                try
                {
                    proc = managedProcs.Single(x => x.Id == processEntry.th32ProcessID); //See if we have a value for that
                }
                catch
                {
                    proc = new Process();
                }

                procs.Add(new MythicProcessInfo()
                {
                    process_id = processEntry.th32ProcessID,
                    parent_process_id = processEntry.th32ParentProcessID,
                    name = processEntry.szExeFile,
                    description = proc.MainWindowTitle,
                    bin_path = proc.MainModule.FileName,
                    start_time = new DateTimeOffset(proc.StartTime).ToUnixTimeMilliseconds(),

                });
            }
            while (Process32Next(snapshotHandle, ref processEntry));

            CloseHandle(snapshotHandle);

            return procs;
        }
    }
}
