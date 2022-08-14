using PluginBase;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
namespace Plugin
{
    public static class getshares
    {
        #region External Calls
        [DllImport("Netapi32.dll", SetLastError = true)]
        static extern int NetApiBufferFree(IntPtr Buffer);
        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetShareEnum(
             StringBuilder ServerName,
             int level,
             ref IntPtr bufPtr,
             uint prefmaxlen,
             ref int entriesread,
             ref int totalentries,
             ref int resume_handle
             );
        #endregion
        #region External Structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHARE_INFO_1
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
            public override string ToString()
            {
                return shi1_netname;
            }
        }
        #endregion
        const uint MAX_PREFERRED_LENGTH = 0xFFFFFFFF;
        const int NERR_Success = 0;
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            StringBuilder sb = new StringBuilder();
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
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "A file was provided but contained no data",
                            task_id = (string)args["task-id"],
                            status = "error",
                        };
                    }
                }
                else
                {
                    targets = args["hosts"].ToString().Split(',');
                }

                if(targets.Count() < 1)
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "No targets provided",
                        task_id = (string)args["task-id"],
                        status = "error",
                    };
                }
                
                
                foreach (var server in targets)
                {
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
                }
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
            }


            return new ResponseResult
            {
                completed = "true",
                user_output = sb.ToString(),
                task_id = (string)args["task-id"],
            };
        }
        public static SHARE_INFO_1[] EnumNetShares(string Server)
        {
            List<SHARE_INFO_1> ShareInfos = new List<SHARE_INFO_1>();
            int entriesread = 0;
            int totalentries = 0;
            int resume_handle = 0;
            int nStructSize = Marshal.SizeOf(typeof(SHARE_INFO_1));
            IntPtr bufPtr = IntPtr.Zero;
            StringBuilder server = new StringBuilder(Server);
            int ret = NetShareEnum(server, 1, ref bufPtr, MAX_PREFERRED_LENGTH, ref entriesread, ref totalentries, ref resume_handle);
            if (ret == NERR_Success)
            {
                IntPtr currentPtr = bufPtr;
                for (int i = 0; i < entriesread; i++)
                {
                    SHARE_INFO_1 shi1 = (SHARE_INFO_1)Marshal.PtrToStructure(currentPtr, typeof(SHARE_INFO_1));
                    ShareInfos.Add(shi1);
                    currentPtr = new IntPtr(currentPtr.ToInt64() + nStructSize);
                    //currentPtr += nStructSize;
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

        private static IEnumerable<string> GetTargetsFromFile(byte[] b)
        {
            string allData = System.Text.Encoding.ASCII.GetString(b);
            
            return allData.Split(Environment.NewLine);
        }

    }
}
