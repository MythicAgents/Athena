// Author: Ryan Cobb (@cobbr_io), The Wover (@TheRealWover)
// Project: SharpSploit (https://github.com/cobbr/SharpSploit)
// License: BSD 3-Clause

using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace Invoker.Dynamic;

/// <summary>
/// Contains function prototypes and wrapper functions for dynamically invoking NT API Calls.
/// </summary>
public static class Native
{
    private const string NTDLL = "ntdll.dll";
    
    public static void RtlInitUnicodeString(ref Data.Native.UNICODE_STRING destinationString,
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString)
    {
        object[] parameters = new object[] { destinationString, sourceString };

        Generic.InvokeApi<object>(
            NTDLL, 
            "RtlInitUnicodeString",
            typeof(RtlInitUnicodeStringDelegate),
            ref parameters);

        destinationString = (Data.Native.UNICODE_STRING)parameters[0];
    }

    public static Data.Native.NTSTATUS LdrLoadDll(IntPtr pathToFile, uint dwFlags,
        ref Data.Native.UNICODE_STRING moduleFileName, ref IntPtr moduleHandle)
    {
        object[] parameters = new object[] {

            pathToFile, dwFlags, moduleFileName, moduleHandle
        };

        var status = Generic.InvokeApi<Data.Native.NTSTATUS>(
            NTDLL, 
            "LdrLoadDll",
            typeof(LdrLoadDllDelegate), 
            ref parameters);

        moduleHandle = (IntPtr)parameters[3];
        return status;
    }

    public static void RtlZeroMemory(IntPtr destination, int length)
    {
        object[] parameters = new object[] { destination, length };

        _ = Generic.InvokeApi<object>(
            NTDLL, 
            "RtlZeroMemory",
            typeof(RtlZeroMemoryDelegate),
            ref parameters);
    }

    public static Data.Native.NTSTATUS NtQueryInformationProcess(IntPtr hProcess,
        Data.Native.PROCESSINFOCLASS processInfoClass, out IntPtr pProcInfo)
    {
        int processInformationLength;
        uint retLen = 0;

        switch (processInfoClass)
        {
            case Data.Native.PROCESSINFOCLASS.ProcessWow64Information:
                pProcInfo = Marshal.AllocHGlobal(IntPtr.Size);
                RtlZeroMemory(pProcInfo, IntPtr.Size);
                processInformationLength = IntPtr.Size;
                break;

            case Data.Native.PROCESSINFOCLASS.ProcessBasicInformation:
                var pbi = new Data.Native.PROCESS_BASIC_INFORMATION();
                pProcInfo = Marshal.AllocHGlobal(Marshal.SizeOf(pbi));
                RtlZeroMemory(pProcInfo, Marshal.SizeOf(pbi));
                Marshal.StructureToPtr(pbi, pProcInfo, true);
                processInformationLength = Marshal.SizeOf(pbi);
                break;

            default:
                throw new InvalidOperationException($"Invalid ProcessInfoClass: {processInfoClass}");
        }

        object[] parameters = new object[] { hProcess, processInfoClass, pProcInfo, processInformationLength, retLen };

        var status = Generic.InvokeApi<Data.Native.NTSTATUS>(
            NTDLL, 
            "NtQueryInformationProcess",
            typeof(NtQueryInformationProcessDelegate), 
            ref parameters);

        pProcInfo = (IntPtr)parameters[2];
        return status;
    }

    public static Data.Native.PROCESS_BASIC_INFORMATION NtQueryInformationProcessBasicInformation(IntPtr hProcess)
    {
        _ = NtQueryInformationProcess(
            hProcess, 
            Data.Native.PROCESSINFOCLASS.ProcessBasicInformation,
            out var pProcInfo);

        return Marshal.PtrToStructure<Data.Native.PROCESS_BASIC_INFORMATION>(pProcInfo);
    }

    public static uint NtProtectVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, ref IntPtr regionSize,
        uint newProtect)
    {
        object[] parameters = new object[] { processHandle, baseAddress, regionSize, newProtect, (uint)0 };

        _ = Generic.InvokeApi<Data.Native.NTSTATUS>(
            NTDLL, 
            "NtProtectVirtualMemory",
            typeof(NtProtectVirtualMemoryDelegate),
            ref parameters);

        return (uint)parameters[4];
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtCreateThreadExDelegate(
        out IntPtr threadHandle,
        Data.Win32.WinNT.ACCESS_MASK desiredAccess,
        IntPtr objectAttributes,
        IntPtr processHandle,
        IntPtr startAddress,
        IntPtr parameter,
        bool createSuspended,
        int stackZeroBits,
        int sizeOfStack,
        int maximumStackSize,
        IntPtr attributeList);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtCreateSectionDelegate(
        ref IntPtr sectionHandle,
        uint desiredAccess,
        IntPtr objectAttributes,
        ref ulong maximumSize,
        uint sectionPageProtection,
        uint allocationAttributes,
        IntPtr fileHandle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtUnmapViewOfSectionDelegate(
        IntPtr hProc,
        IntPtr baseAddr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtMapViewOfSectionDelegate(
        IntPtr sectionHandle,
        IntPtr processHandle,
        out IntPtr baseAddress,
        IntPtr zeroBits,
        IntPtr commitSize,
        IntPtr sectionOffset,
        out ulong viewSize,
        uint inheritDisposition,
        uint allocationType,
        uint win32Protect);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint LdrLoadDllDelegate(
        IntPtr pathToFile,
        uint dwFlags,
        ref Data.Native.UNICODE_STRING moduleFileName,
        ref IntPtr moduleHandle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void RtlInitUnicodeStringDelegate(
        ref Data.Native.UNICODE_STRING destinationString,
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void RtlZeroMemoryDelegate(
        IntPtr destination,
        int length);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtQueryInformationProcessDelegate(
        IntPtr processHandle,
        Data.Native.PROCESSINFOCLASS processInformationClass,
        IntPtr processInformation,
        int processInformationLength,
        ref uint returnLength);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtAllocateVirtualMemoryDelegate(
        IntPtr processHandle,
        ref IntPtr baseAddress,
        IntPtr zeroBits,
        ref IntPtr regionSize,
        uint allocationType,
        uint protect);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtFreeVirtualMemoryDelegate(
        IntPtr processHandle,
        ref IntPtr baseAddress,
        ref IntPtr regionSize,
        uint freeType);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtProtectVirtualMemoryDelegate(
        IntPtr processHandle,
        ref IntPtr baseAddress,
        ref IntPtr regionSize,
        uint newProtect,
        ref uint oldProtect);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtWriteVirtualMemoryDelegate(
        IntPtr processHandle,
        IntPtr baseAddress,
        IntPtr buffer,
        uint bufferLength,
        ref uint bytesWritten);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint LdrGetProcedureAddressDelegate(
        IntPtr hModule,
        IntPtr functionName,
        IntPtr ordinal,
        ref IntPtr functionAddress);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint RtlGetVersionDelegate(
        ref Data.Native.OSVERSIONINFOEX versionInformation);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtOpenFileDelegate(
        ref IntPtr fileHandle,
        Data.Win32.Kernel32.FileAccessFlags accessFlags,
        ref Data.Native.OBJECT_ATTRIBUTES objectAttributes,
        ref Data.Native.IO_STATUS_BLOCK ioStatusBlock,
        Data.Win32.Kernel32.FileShareFlags shareAccess,
        Data.Win32.Kernel32.FileOpenFlags openOptions);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NtCloseDelegate(
        IntPtr handle);
}