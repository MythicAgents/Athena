using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Agent
{
    public class Native
    {
        [Flags]
        public enum LogonType
        {
            LOGON32_LOGON_INTERACTIVE = 2,
            LOGON32_LOGON_NETWORK = 3,
            LOGON32_LOGON_BATCH = 4,
            LOGON32_LOGON_SERVICE = 5,
            LOGON32_LOGON_UNLOCK = 7,
            LOGON32_LOGON_NETWORK_CLEARTEXT = 8,
            LOGON32_LOGON_NEW_CREDENTIALS = 9
        }

        [Flags]
        public enum LogonProvider
        {
            LOGON32_PROVIDER_DEFAULT = 0,
            LOGON32_PROVIDER_WINNT35,
            LOGON32_PROVIDER_WINNT40,
            LOGON32_PROVIDER_WINNT50
        }
        internal enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Boolean LogonUser(
            String lpszUserName,
            String lpszDomain,
            String lpszPassword,
            LogonType dwLogonType,
            LogonProvider dwLogonProvider,
            out SafeAccessTokenHandle phToken);

        //[DllImport("advapi32.dll", SetLastError = true)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //internal static extern bool OpenProcessToken(IntPtr ProcessHandle,
        //    uint desiredAccess, out SafeAccessTokenHandle TokenHandle);


        //[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //internal extern static bool DuplicateTokenEx(
        //    IntPtr hExistingToken,
        //    uint dwDesiredAccess,
        //    IntPtr lpTokenAttributes,
        //    uint ImpersonationLevel,
        //    TOKEN_TYPE TokenType,
        //    out SafeAccessTokenHandle phNewToken);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //static extern bool CloseHandle(IntPtr hObject);
    }
}
