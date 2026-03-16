using System.Runtime.InteropServices;

namespace recon
{
    internal static class ReconNative
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsGetCacheDataTable")]
        internal static extern bool DnsGetCacheDataTable(out IntPtr ppEntry);

        [StructLayout(LayoutKind.Sequential)]
        internal struct DNS_CACHE_ENTRY
        {
            public IntPtr pNext;
            public IntPtr pszName;
            public ushort wType;
            public ushort wDataLength;
            public uint dwFlags;
        }
    }
}
