using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public static class Native
    {
        public const int PROC_PIDPATHINFO_MAXSIZE = 4 * 1024; // Max size of process path

        // Define the libproc functions
        [DllImport("libproc.dylib", CallingConvention = CallingConvention.Cdecl)]
        public static extern int proc_pidpath(int pid, IntPtr buffer, uint bufsize);

        [DllImport("libproc.dylib", CallingConvention = CallingConvention.Cdecl)]
        public static extern int proc_pidinfo(int pid, int flavor, ulong arg, IntPtr buffer, int buffersize);

        // Define the proc_info structure for task_info
        [StructLayout(LayoutKind.Sequential)]
        public struct proc_taskinfo
        {
            public uint pti_virtual_size;
            public uint pti_resident_size;
            public uint pti_total_user;
            public uint pti_total_system;
            public uint pti_threads_user;
            public uint pti_threads_system;
            public int pti_policy;
            public int pti_faults;
            public int pti_pageins;
            public int pti_cow_faults;
            public int pti_messages_sent;
            public int pti_messages_received;
            public int pti_syscalls_mach;
            public int pti_syscalls_unix;
            public int pti_csw;
            public int pti_threadnum;
            public int pti_numrunning;
            public int pti_priority;
        }

        // Define the proc_bsdinfo structure for proc_pidinfo
        [StructLayout(LayoutKind.Sequential)]
        struct proc_bsdinfo
        {
            public int pbi_flags;
            public int pbi_status;
            public int pbi_xstatus;
            public int pbi_pid;
            public int pbi_ppid;
            public int pbi_uid;
            public int pbi_gid;
            public int pbi_ruid;
            public int pbi_rgid;
            public int pbi_svuid;
            public int pbi_svgid;
            public IntPtr pbi_comm;
            public IntPtr pbi_name;
            public int pbi_nfiles;
            public int pbi_pgid;
            public int pbi_pjobc;
            public int e_tdev;
            public int e_tpgid;
            public ulong pbi_nspg;
            public ulong pbi_nlp;
            public ulong pbi_fspg;
            public ulong pbi_flpg;
            public ulong pbi_tspg;
            public ulong pbi_tlpg;
            public ulong pbi_policy;
        }

        // Define the PROC_PIDLISTFDS flavor
        private const int PROC_PIDLISTFDS = 1;

        // Define the PROC_PIDFDINFO flavor
        private const int PROC_PIDFDINFO = 6;

        // Define the PROC_PIDFDSIZE flavor
        private const int PROC_PIDFDSIZE = 7;

        // Define the PROC_PIDPATHINFO flavor
        private const int PROC_PIDPATHINFO = 3;

        // Define the PROC_PIDTASKINFO flavor
        private const int PROC_PIDTASKINFO = 4;

        // Define the PROC_PIDTBSDINFO flavor
        private const int PROC_PIDTBSDINFO = 5;

        // Define the PROC_PIDTHREADINFO flavor
        private const int PROC_PIDTHREADINFO = 7;

        // Define the PROC_PIDREGIONINFO flavor
        private const int PROC_PIDREGIONINFO = 10;

        // Define the PROC_PIDLISTTHREADS flavor
        private const int PROC_PIDLISTTHREADS = 5;

        // Define the PROC_PIDREGIONPATHINFO flavor
        private const int PROC_PIDREGIONPATHINFO = 12;
    }
}
