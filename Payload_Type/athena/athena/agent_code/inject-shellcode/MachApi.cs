using System.Runtime.InteropServices;

namespace Workflow
{
    public static class MachApi
    {
        [DllImport("libSystem.B.dylib")]
        public static extern uint mach_task_self();

        [DllImport("libSystem.B.dylib")]
        public static extern int task_for_pid(
            uint target_tport, int pid, out uint task);

        [DllImport("libSystem.B.dylib")]
        public static extern int mach_vm_allocate(
            uint target_task, ref ulong address, ulong size, int flags);

        [DllImport("libSystem.B.dylib")]
        public static extern int mach_vm_write(
            uint target_task, ulong address, byte[] data, uint dataCnt);

        [DllImport("libSystem.B.dylib")]
        public static extern int mach_vm_protect(
            uint target_task, ulong address, ulong size, int set_maximum, int new_protection);

        [DllImport("libSystem.B.dylib")]
        public static extern int thread_create_running(
            uint target_task, int flavor, ulong[] new_state, uint new_stateCnt, out uint child_act);

        public const int VM_FLAGS_ANYWHERE = 1;
        public const int VM_PROT_READ = 1;
        public const int VM_PROT_EXECUTE = 4;
        public const int VM_PROT_READ_EXECUTE = VM_PROT_READ | VM_PROT_EXECUTE;

        public const int x86_THREAD_STATE64 = 4;
        public const uint x86_THREAD_STATE64_COUNT = 42;

        public const int ARM_THREAD_STATE64 = 6;
        public const uint ARM_THREAD_STATE64_COUNT = 68;
    }
}
