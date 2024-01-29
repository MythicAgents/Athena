using Agent.Interfaces;
using Agent.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "win-enum-resources";
        [DllImport("mpr.dll", CharSet = CharSet.Auto)]
        public static extern int WNetEnumResource(
            IntPtr hEnum,
            ref int lpcCount,
            IntPtr lpBuffer,
            ref int lpBufferSize);

        [DllImport("mpr.dll", CharSet = CharSet.Auto)]
        public static extern int WNetOpenEnum(
            RESOURCE_SCOPE dwScope,
            RESOURCE_TYPE dwType,
            RESOURCE_USAGE dwUsage,
            [MarshalAs(UnmanagedType.AsAny)][In] Object lpNetResource,
            out IntPtr lphEnum);

        [DllImport("mpr.dll", CharSet = CharSet.Auto)]
        public static extern int WNetCloseEnum(IntPtr hEnum);
        //declare the structures to hold info

        public enum RESOURCE_SCOPE
        {
            RESOURCE_CONNECTED = 0x00000001,
            RESOURCE_GLOBALNET = 0x00000002,
            RESOURCE_REMEMBERED = 0x00000003,
            RESOURCE_RECENT = 0x00000004,
            RESOURCE_CONTEXT = 0x00000005
        }

        public enum RESOURCE_TYPE
        {
            RESOURCETYPE_ANY = 0x00000000,
            RESOURCETYPE_DISK = 0x00000001,
            RESOURCETYPE_PRINT = 0x00000002,
            RESOURCETYPE_RESERVED = 0x00000008,
        }

        public enum RESOURCE_USAGE
        {
            RESOURCEUSAGE_CONNECTABLE = 0x00000001,
            RESOURCEUSAGE_CONTAINER = 0x00000002,
            RESOURCEUSAGE_NOLOCALDEVICE = 0x00000004,
            RESOURCEUSAGE_SIBLING = 0x00000008,
            RESOURCEUSAGE_ATTACHED = 0x00000010,
            RESOURCEUSAGE_ALL = (RESOURCEUSAGE_CONNECTABLE | RESOURCEUSAGE_CONTAINER | RESOURCEUSAGE_ATTACHED),
        }

        public enum RESOURCE_DISPLAYTYPE
        {
            RESOURCEDISPLAYTYPE_GENERIC = 0x00000000,
            RESOURCEDISPLAYTYPE_DOMAIN = 0x00000001,
            RESOURCEDISPLAYTYPE_SERVER = 0x00000002,
            RESOURCEDISPLAYTYPE_SHARE = 0x00000003,
            RESOURCEDISPLAYTYPE_FILE = 0x00000004,
            RESOURCEDISPLAYTYPE_GROUP = 0x00000005,
            RESOURCEDISPLAYTYPE_NETWORK = 0x00000006,
            RESOURCEDISPLAYTYPE_ROOT = 0x00000007,
            RESOURCEDISPLAYTYPE_SHAREADMIN = 0x00000008,
            RESOURCEDISPLAYTYPE_DIRECTORY = 0x00000009,
            RESOURCEDISPLAYTYPE_TREE = 0x0000000A,
            RESOURCEDISPLAYTYPE_NDSCONTAINER = 0x0000000B
        }
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        internal struct NETRESOURCE
        {
            public RESOURCE_SCOPE dwScope;
            public RESOURCE_TYPE dwType;
            public RESOURCE_DISPLAYTYPE dwDisplayType;
            public RESOURCE_USAGE dwUsage;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpLocalName;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpRemoteName;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpComment;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] public string lpProvider;
        }
        internal string WNETOE(Object o)
        {
            int iRet;
            IntPtr ptrHandle = new IntPtr();
            StringBuilder sb = new StringBuilder();
            try
            {
                iRet = WNetOpenEnum(
                RESOURCE_SCOPE.RESOURCE_GLOBALNET,
                RESOURCE_TYPE.RESOURCETYPE_ANY,
                RESOURCE_USAGE.RESOURCEUSAGE_ALL,
                o,
                out ptrHandle);
                if (iRet != 0)
                {
                    return "Couldn't start Enum: " + iRet.ToString();
                }

                int entries;
                int buffer = 16384;
                IntPtr ptrBuffer = Marshal.AllocHGlobal(buffer);
                NETRESOURCE nr;
                for (; ; )
                {
                    entries = -1;
                    buffer = 16384;
                    iRet = WNetEnumResource(ptrHandle, ref entries, ptrBuffer, ref buffer);
                    if ((iRet != 0) || (entries < 1))
                    {
                        break;
                    }
                    IntPtr ptr = ptrBuffer;
                    for (int i = 0; i < entries; i++)
                    {
                        nr = (NETRESOURCE)Marshal.PtrToStructure(ptr, typeof(NETRESOURCE));
                        if (RESOURCE_USAGE.RESOURCEUSAGE_CONTAINER == (nr.dwUsage
                            & RESOURCE_USAGE.RESOURCEUSAGE_CONTAINER))
                        {
                            //call recursively to get all entries in a container
                            WNETOE(nr);
                        }
                        ptr += Marshal.SizeOf(nr);
                        sb.AppendFormat(" {0} : LocalName='{1}' RemoteName='{2}'",
                        nr.dwDisplayType.ToString(), nr.lpLocalName, nr.lpRemoteName).AppendLine();
                    }
                }
                Marshal.FreeHGlobal(ptrBuffer);
                iRet = WNetCloseEnum(ptrHandle);
            }
            catch (Exception e)
            {
                sb.AppendLine(e.ToString());
            }

            return sb.ToString();
        }

        //here's some possible error codes
        internal enum NERR
        {
            NERR_Success = 0,/* Success */
            ERROR_MORE_DATA = 234, // dderror
            ERROR_NO_BROWSER_SERVERS_FOUND = 6118,
            ERROR_INVALID_LEVEL = 124,
            ERROR_ACCESS_DENIED = 5,
            ERROR_INVALID_PARAMETER = 87,
            ERROR_NOT_ENOUGH_MEMORY = 8,
            ERROR_NETWORK_BUSY = 54,
            ERROR_BAD_NETPATH = 53,
            ERROR_NO_NETWORK = 1222,
            ERROR_INVALID_HANDLE_STATE = 1609,
            ERROR_EXTENDED_ERROR = 1208
        }
        public async Task Execute(ServerJob job)
        {
            if (job.task.token != 0)
            {
                tokenManager.Impersonate(job.task.token);
            }
            messageManager.Write(WNETOE(null), job.task.id, true);
            if (job.task.token != 0)
            {
                tokenManager.Revert();
            }
        }
    }
}