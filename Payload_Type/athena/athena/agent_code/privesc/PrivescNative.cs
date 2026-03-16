using System.Runtime.InteropServices;

namespace privesc
{
    internal static class PrivescNative
    {
        // Token privileges
        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool OpenProcessToken(
            IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool GetTokenInformation(
            IntPtr TokenHandle, int TokenInformationClass,
            IntPtr TokenInformation, int TokenInformationLength,
            out int ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool LookupPrivilegeName(
            string? lpSystemName, ref LUID lpLuid,
            System.Text.StringBuilder lpName, ref int cchName);

        // Service control
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr OpenSCManager(
            string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool EnumServicesStatusEx(
            IntPtr hSCManager, int InfoLevel, uint dwServiceType,
            uint dwServiceState, IntPtr lpServices, int cbBufSize,
            out int pcbBytesNeeded, out int lpServicesReturned,
            ref int lpResumeHandle, string? pszGroupName);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr OpenService(
            IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool QueryServiceConfig(
            IntPtr hService, IntPtr lpServiceConfig, int cbBufSize,
            out int pcbBytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);

        internal const uint TOKEN_QUERY = 0x0008;
        internal const int TokenPrivileges = 3;
        internal const int TokenIntegrityLevel = 25;
        internal const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;
        internal const uint SERVICE_QUERY_CONFIG = 0x0001;
        internal const uint SERVICE_WIN32 = 0x00000030;
        internal const uint SERVICE_STATE_ALL = 0x00000003;

        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SID_AND_ATTRIBUTES
        {
            public IntPtr Sid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_MANDATORY_LABEL
        {
            public SID_AND_ATTRIBUTES Label;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct ENUM_SERVICE_STATUS_PROCESS
        {
            public IntPtr lpServiceName;
            public IntPtr lpDisplayName;
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
            public uint dwProcessId;
            public uint dwServiceFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct QUERY_SERVICE_CONFIG
        {
            public uint dwServiceType;
            public uint dwStartType;
            public uint dwErrorControl;
            public IntPtr lpBinaryPathName;
            public IntPtr lpLoadOrderGroup;
            public uint dwTagId;
            public IntPtr lpDependencies;
            public IntPtr lpServiceStartName;
            public IntPtr lpDisplayName;
        }

        // Privilege attribute flags
        internal const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        internal const uint SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;
    }
}
