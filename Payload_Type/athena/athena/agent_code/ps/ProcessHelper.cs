
using Agent.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Agent
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
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const int MAX_PATH = 260;

        public static List<ServerProcessInfo> GetProcessesWithParent()
        {
            List<ServerProcessInfo> returnProcs = new List<ServerProcessInfo>();
            IntPtr snapshotHandle = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshotHandle == IntPtr.Zero)
            {
                throw new Exception("Failed to create snapshot of processes.");
            }

            PROCESSENTRY32 processEntry = new PROCESSENTRY32();
            processEntry.dwSize = (uint)Marshal.SizeOf(processEntry);

            if (!Process32First(snapshotHandle, ref processEntry))
            {
                CloseHandle(snapshotHandle);
                throw new Exception("Failed to retrieve the first process entry.");
            }
            Process[] procs = Process.GetProcesses();
            do
            {
                Process proc = procs.Single(x => x.Id == (int)processEntry.th32ProcessID);

                try
                {
                    returnProcs.Add(new ServerProcessInfo()
                    {
                        parent_process_id = (int)processEntry.th32ParentProcessID,
                        process_id = (int)processEntry.th32ProcessID,
                        name = processEntry.szExeFile,
                        bin_path = proc.MainModule.FileName,
                        description = proc.MainWindowTitle

                    });
                }
                catch
                {
                    returnProcs.Add(new ServerProcessInfo()
                    {
                        parent_process_id = (int)processEntry.th32ParentProcessID,
                        process_id = (int)processEntry.th32ProcessID,
                        name = processEntry.szExeFile,
                        description = proc.MainWindowTitle
                    });
                }

                //Console.WriteLine($"Process Name: {processEntry.szExeFile}");
                //Console.WriteLine($"Process ID: {processEntry.th32ProcessID}");
                //Console.WriteLine($"Parent Process ID: {processEntry.th32ParentProcessID}");
                //Console.WriteLine($"Process Path: {proc.MainModule.FileName}");
                //Console.WriteLine($"Process Arguments: {proc.StartInfo.Arguments}");

                //try
                //{
                //    Process parentProcess = GetParentProcess(processEntry.th32ParentProcessID);
                //    Console.WriteLine($"Parent Process Name: {parentProcess.ProcessName}");
                //    Console.WriteLine($"Parent Process ID: {parentProcess.Id}");
                //}
                //catch (Exception)
                //{
                //    Console.WriteLine("No parent process found.");
                //}

                //Console.WriteLine("-----------------------------------------");
            }
            while (Process32Next(snapshotHandle, ref processEntry));

            CloseHandle(snapshotHandle);
            return returnProcs;
        }

        private static Process GetParentProcess(uint parentProcessId)
        {
            IntPtr parentProcessHandle = OpenProcess(PROCESS_QUERY_INFORMATION, false, parentProcessId);
            if (parentProcessHandle == IntPtr.Zero)
            {
                throw new Exception("Failed to open parent process.");
            }

            char[] exeName = new char[MAX_PATH];
            int exeNameSize = MAX_PATH;
            if (!QueryFullProcessImageName(parentProcessHandle, 0, exeName, ref exeNameSize))
            {
                CloseHandle(parentProcessHandle);
                throw new Exception("Failed to retrieve parent process image name.");
            }

            CloseHandle(parentProcessHandle);

            string exePath = new string(exeName, 0, exeNameSize);
            return Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(exePath))[0];
        }
    }
}
