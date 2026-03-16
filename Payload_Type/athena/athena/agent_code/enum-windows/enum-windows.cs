using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Workflow.Contracts;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "enum-windows";

        // --- get-localgroup P/Invoke ---
        [DllImport("NetAPI32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetLocalGroupGetMembers(
            [MarshalAs(UnmanagedType.LPWStr)] string servername,
            [MarshalAs(UnmanagedType.LPWStr)] string localgroupname,
            int level,
            out IntPtr bufptr,
            int prefmaxlen,
            out int entriesread,
            out int totalentries,
            IntPtr resume_handle);

        [DllImport("Netapi32.dll")]
        private static extern int NetLocalGroupEnum(
            [MarshalAs(UnmanagedType.LPWStr)] string servername,
            int level,
            out IntPtr bufptr,
            int prefmaxlen,
            out int entriesread,
            out int totalentries,
            ref int resume_handle);

        // --- get-sessions P/Invoke ---
        [DllImport("netapi32.dll", SetLastError = true)]
        private static extern int NetSessionEnum(
            [In, MarshalAs(UnmanagedType.LPWStr)] string ServerName,
            [In, MarshalAs(UnmanagedType.LPWStr)] string? UncClientName,
            [In, MarshalAs(UnmanagedType.LPWStr)] string? UserName,
            Int32 Level,
            out IntPtr bufptr,
            int prefmaxlen,
            ref Int32 entriesread,
            ref Int32 totalentries,
            ref Int32 resume_handle);

        // --- get-shares P/Invoke ---
        [DllImport("Netapi32.dll", SetLastError = true)]
        private static extern int NetShareEnum(
            StringBuilder ServerName,
            int level,
            ref IntPtr bufPtr,
            uint prefmaxlen,
            ref int entriesread,
            ref int totalentries,
            ref int resume_handle);

        [DllImport("Netapi32.dll")]
        private static extern int NetApiBufferFree(IntPtr buffer);

        // --- Structs: get-localgroup ---
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LOCALGROUP_MEMBERS_INFO_2
        {
            public IntPtr lgrmi2_sid;
            public int lgrmi2_sidusage;
            public string lgrmi2_domainandname;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LOCALGROUP_USERS_INFO_0
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LOCALGROUP_USERS_INFO_1
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string name;
            [MarshalAs(UnmanagedType.LPWStr)] public string comment;
        }

        // --- Structs: get-sessions ---
        [StructLayout(LayoutKind.Sequential)]
        private struct SESSION_INFO_10
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string sesi10_cname;
            [MarshalAs(UnmanagedType.LPWStr)] public string sesi10_username;
            public uint sesi10_time;
            public uint sesi10_idle_time;
        }

        // --- Enum: get-sessions NERR ---
        private enum NERR
        {
            NERR_Success = 0,
            ERROR_MORE_DATA = 234,
            ERROR_NO_BROWSER_SERVERS_FOUND = 6118,
            ERROR_INVALID_LEVEL = 124,
            ERROR_ACCESS_DENIED = 5,
            ERROR_INVALID_PARAMETER = 87,
            ERROR_NOT_ENOUGH_MEMORY = 8,
            ERROR_NETWORK_BUSY = 54,
            ERROR_BAD_NETPATH = 53,
            ERROR_NO_NETWORK = 1222,
            ERROR_INVALID_HANDLE_STATE = 1609,
            ERROR_EXTENDED_ERROR = 1208,
            NERR_BASE = 2100,
            NERR_UnknownDevDir = (NERR_BASE + 16),
            NERR_DuplicateShare = (NERR_BASE + 18),
            NERR_BufTooSmall = (NERR_BASE + 23)
        }

        // --- Structs: get-shares ---
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHARE_INFO_1
        {
            public string shi1_netname;
            public uint shi1_type;
            public string shi1_remark;
            public SHARE_INFO_1(string sharename, uint sharetype, string remark)
            {
                this.shi1_netname = sharename;
                this.shi1_type = sharetype;
                this.shi1_remark = remark;
            }
            public override string ToString() => shi1_netname;
        }

        // --- get-localgroup constants ---
        private readonly int NERR_Success = 0;
        private static readonly int ERROR_ACCESS_DENIED = 5;
        private static readonly int ERROR_MORE_DATA = 234;
        private static readonly int NERR_Base = 2100;
        private static readonly int NERR_InvalidComputer = NERR_Base + 251;
        private static readonly int NERR_BufTooSmall = NERR_Base + 23;

        // --- get-shares constants ---
        private const uint MAX_PREFERRED_LENGTH = 0xFFFFFFFF;
        private const int NERR_Success_Shares = 0;

        private IDataBroker messageManager { get; set; }
        private ICredentialProvider tokenManager { get; set; }

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
            this.tokenManager = context.TokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            enum_windows.EnumWindowsArgs? args =
                JsonSerializer.Deserialize<enum_windows.EnumWindowsArgs>(job.task.parameters);

            if (args is null)
            {
                DebugLog.Log($"{Name} args null, returning [{job.task.id}]");
                messageManager.Write("Failed to parse arguments.", job.task.id, true, "error");
                return;
            }

            switch (args.action)
            {
                case "get-localgroup":
                    await ExecuteGetLocalGroup(args, job);
                    break;
                case "get-sessions":
                    await ExecuteGetSessions(args, job);
                    break;
                case "get-shares":
                    await ExecuteGetShares(args, job);
                    break;
                default:
                    messageManager.Write($"Unknown action: {args.action}", job.task.id, true, "error");
                    break;
            }

            DebugLog.Log($"{Name} completed [{job.task.id}]");
        }

        // ---- get-localgroup ----

        private async Task ExecuteGetLocalGroup(enum_windows.EnumWindowsArgs args, ServerJob job)
        {
            TaskResponse rr = new TaskResponse();
            rr.task_id = job.task.id;

            if (!String.IsNullOrEmpty(args.group))
            {
                DebugLog.Log($"{Name} querying group '{args.group}' on '{args.hostname ?? "localhost"}' [{job.task.id}]");
                if (!String.IsNullOrEmpty(args.hostname))
                {
                    rr.user_output = String.Join(Environment.NewLine,
                        GetLocalGroupMembers(args.hostname, args.group));
                }
                else
                {
                    rr.user_output = String.Join(Environment.NewLine,
                        GetLocalGroupMembers(null, args.group));
                }
            }
            else
            {
                DebugLog.Log($"{Name} enumerating all groups on '{args.hostname ?? "localhost"}' [{job.task.id}]");
                if (!String.IsNullOrEmpty(args.hostname))
                {
                    rr.user_output = String.Join(Environment.NewLine,
                        GetAllLocalGroups(args.hostname));
                }
                else
                {
                    rr.user_output = String.Join(Environment.NewLine,
                        GetAllLocalGroups(null));
                }
            }

            rr.completed = true;
            messageManager.AddTaskResponse(rr);
        }

        private List<string> GetLocalGroupMembers(string? ServerName, string GroupName)
        {
            List<string> myList = new List<string>();
            int EntriesRead;
            int TotalEntries;
            IntPtr Resume = IntPtr.Zero;
            IntPtr bufPtr;
#pragma warning disable CS8604
            int val = NetLocalGroupGetMembers(ServerName, GroupName, 2, out bufPtr, -1,
                out EntriesRead, out TotalEntries, Resume);
#pragma warning restore CS8604
            if (EntriesRead > 0)
            {
                LOCALGROUP_MEMBERS_INFO_2[] Members = new LOCALGROUP_MEMBERS_INFO_2[EntriesRead];
                IntPtr iter = bufPtr;
                for (int i = 0; i < EntriesRead; i++)
                {
#pragma warning disable CS8605
                    Members[i] = (LOCALGROUP_MEMBERS_INFO_2)Marshal.PtrToStructure(iter,
                        typeof(LOCALGROUP_MEMBERS_INFO_2));
#pragma warning restore CS8605
                    iter = (IntPtr)((long)iter + Marshal.SizeOf(typeof(LOCALGROUP_MEMBERS_INFO_2)));
                    myList.Add(Members[i].lgrmi2_domainandname);
                }
                NetApiBufferFree(bufPtr);
            }
            return myList;
        }

        private List<string> GetAllLocalGroups(string? serverName)
        {
            int res = 0;
            int level = 1;
            IntPtr buffer = IntPtr.Zero;
            int MAX_PREFERRED_LENGTH_LOCAL = -1;
            int read, total;
            int handle = 0;

            var groups = new List<string>();
            try
            {
#pragma warning disable CS8604
                res = NetLocalGroupEnum(serverName, level, out buffer, MAX_PREFERRED_LENGTH_LOCAL,
                    out read, out total, ref handle);
#pragma warning restore CS8604

                if (res != NERR_Success)
                {
                    return new List<string>() { $"NetLocalGroupEnum failed: {res}" };
                }

                IntPtr ptr = buffer;
                for (int i = 0; i < read; i++)
                {
#pragma warning disable CS8605
                    var group = (LOCALGROUP_USERS_INFO_1)Marshal.PtrToStructure(ptr,
                        typeof(LOCALGROUP_USERS_INFO_1));
#pragma warning restore CS8605
                    groups.Add($"{group.name}\t\t\t{group.comment}");
                    ptr = (IntPtr)((long)ptr + Marshal.SizeOf(typeof(LOCALGROUP_USERS_INFO_1)));
                }
            }
            finally
            {
                NetApiBufferFree(buffer);
            }

            return groups;
        }

        // ---- get-sessions ----

        private async Task ExecuteGetSessions(enum_windows.EnumWindowsArgs args, ServerJob job)
        {
            try
            {
                string[] targets = ResolveTargets(args, job);
                if (targets.Length == 0) return;

                foreach (var server in targets)
                {
                    StringBuilder sb = new StringBuilder();
                    try
                    {
                        IntPtr BufPtr;
                        int res = 0;
                        Int32 er = 0, tr = 0, resume = 0;
                        BufPtr = (IntPtr)Marshal.SizeOf(typeof(SESSION_INFO_10));
                        SESSION_INFO_10[] results = new SESSION_INFO_10[0];
                        do
                        {
                            res = NetSessionEnum(server, null, null, 10, out BufPtr, -1,
                                ref er, ref tr, ref resume);
                            results = new SESSION_INFO_10[er];
                            if (res == (int)NERR.ERROR_MORE_DATA || res == (int)NERR.NERR_Success)
                            {
                                long p = BufPtr.ToInt64();
                                for (int i = 0; i < er; i++)
                                {
#pragma warning disable CS8605
                                    SESSION_INFO_10 si = (SESSION_INFO_10)Marshal.PtrToStructure(
                                        new IntPtr(p), typeof(SESSION_INFO_10));
#pragma warning restore CS8605
                                    results[i] = si;
                                    p += Marshal.SizeOf(typeof(SESSION_INFO_10));
                                }
                            }
                            Marshal.FreeHGlobal(BufPtr);
                        }
                        while (res == (int)NERR.ERROR_MORE_DATA);

                        int sess = 0;
                        sb.AppendLine("Sessions for: " + server);
                        foreach (var result in results)
                        {
                            sb.AppendLine($"SessionID: {sess}");
                            sb.AppendLine("---------------------------------------");
                            sb.AppendLine($"Username: {result.sesi10_username}");
                            sb.AppendLine($"From: {result.sesi10_cname}");
                            sb.AppendLine($"Time Active: {result.sesi10_time}");
                            sb.AppendLine($"Time Idle: {result.sesi10_idle_time}");
                            sb.AppendLine("---------------------------------------\r\n");
                            sb.AppendLine();
                            sess++;
                        }

                        messageManager.Write(sb.ToString(), job.task.id, false);
                    }
                    catch (Exception e)
                    {
                        messageManager.Write(e.ToString(), job.task.id, true, "error");
                    }
                    Thread.Sleep(10000);
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} exception: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }

            messageManager.Write("Execution Finished.", job.task.id, true);
        }

        // ---- get-shares ----

        private async Task ExecuteGetShares(enum_windows.EnumWindowsArgs args, ServerJob job)
        {
            try
            {
                string[] targets = ResolveTargets(args, job);
                if (targets.Length == 0) return;

                foreach (var server in targets)
                {
                    StringBuilder sb = new StringBuilder();
                    try
                    {
                        SHARE_INFO_1[] shares = EnumNetShares(server);
                        sb.AppendLine($"List of Shares on {server}:\r\n");
                        sb.AppendLine("Share Name\tComment\t\tType");
                        sb.AppendLine("--------------------------------------------");
                        foreach (SHARE_INFO_1 s in shares)
                        {
                            sb.AppendLine($"{s.shi1_netname}\t\t{s.shi1_remark}\t{s.shi1_type}");
                        }
                    }
                    catch (Exception e)
                    {
                        sb.AppendLine(server);
                        sb.AppendLine(e.ToString());
                    }
                    sb.AppendLine();
                    messageManager.Write(sb.ToString(), job.task.id, false);
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} exception: {e.Message} [{job.task.id}]");
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }

            messageManager.Write("Finished executing.", job.task.id, true);
        }

        private SHARE_INFO_1[] EnumNetShares(string Server)
        {
            List<SHARE_INFO_1> ShareInfos = new List<SHARE_INFO_1>();
            int entriesread = 0;
            int totalentries = 0;
            int resume_handle = 0;
            int nStructSize = Marshal.SizeOf(typeof(SHARE_INFO_1));
            IntPtr bufPtr = IntPtr.Zero;
            StringBuilder server = new StringBuilder(Server);
            int ret = NetShareEnum(server, 1, ref bufPtr, MAX_PREFERRED_LENGTH,
                ref entriesread, ref totalentries, ref resume_handle);
            if (ret == NERR_Success_Shares)
            {
                IntPtr currentPtr = bufPtr;
                for (int i = 0; i < entriesread; i++)
                {
#pragma warning disable CS8605
                    SHARE_INFO_1 shi1 = (SHARE_INFO_1)Marshal.PtrToStructure(currentPtr,
                        typeof(SHARE_INFO_1));
#pragma warning restore CS8605
                    ShareInfos.Add(shi1);
                    currentPtr = new IntPtr(currentPtr.ToInt64() + nStructSize);
                }
                NetApiBufferFree(bufPtr);
                return ShareInfos.ToArray();
            }
            else
            {
                ShareInfos.Add(new SHARE_INFO_1("ERROR CODE = " + ret.ToString(), 0, string.Empty));
                return ShareInfos.ToArray();
            }
        }

        // ---- shared helpers ----

        private string[] ResolveTargets(enum_windows.EnumWindowsArgs args, ServerJob job)
        {
            if (!string.IsNullOrEmpty(args.targetlist))
            {
                byte[] fileBytes = Convert.FromBase64String(args.targetlist);
                return GetTargetsFromFile(fileBytes).ToArray();
            }

            if (!string.IsNullOrEmpty(args.hosts))
            {
                return args.hosts.Split(',');
            }

            messageManager.Write("No targets provided.", job.task.id, true, "error");
            return Array.Empty<string>();
        }

        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);
            return allData.Split(Environment.NewLine);
        }
    }
}
