using System;
using System.Runtime.InteropServices;

namespace NidhoggCSharpApi
{
    public class NidhoggApiException : Exception
    {
        public NidhoggApiException(string message) : base(message)
        {
        }
    }

    internal partial class NidhoggApi
    {
        private NidhoggErrorCodes lastError;
        private IntPtr hNidhogg;
        private const string DRIVER_NAME = "\\\\.\\Nidhogg";
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;
        private const int INVALID_HANDLE_VALUE = -1;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);


        public NidhoggApi()
        {
            lastError = NidhoggErrorCodes.NIDHOGG_SUCCESS;
            hNidhogg = CreateFileW(DRIVER_NAME, GENERIC_WRITE | GENERIC_READ, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            if (hNidhogg == new IntPtr(INVALID_HANDLE_VALUE) || hNidhogg == IntPtr.Zero)
                throw new NidhoggApiException("Failed to connect to Nidhogg driver");
        }
        public NidhoggApi(string driverName)
        {
            lastError = NidhoggErrorCodes.NIDHOGG_SUCCESS;
            hNidhogg = CreateFileW(driverName, GENERIC_WRITE | GENERIC_READ, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            if (hNidhogg == new IntPtr(INVALID_HANDLE_VALUE) || hNidhogg == IntPtr.Zero)
                throw new NidhoggApiException("Failed to connect to Nidhogg driver");
        }

        ~NidhoggApi()
        {
            if (hNidhogg != IntPtr.Zero && hNidhogg != new IntPtr(INVALID_HANDLE_VALUE))
                CloseHandle(hNidhogg);
        }

        public NidhoggErrorCodes ExecuteScript(IntPtr script, uint scriptSize)
        {
            if (script == IntPtr.Zero || scriptSize == 0)
                return NidhoggErrorCodes.NIDHOGG_INVALID_INPUT;

            ScriptInformation scriptInformation = new ScriptInformation
            {
                Script = script,
                ScriptSize = scriptSize
            };

            return NidhoggSendDataIoctl(scriptInformation, IOCTL_EXEC_SCRIPT);
        }
    }
}