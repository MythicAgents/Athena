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
    
    public static Data.Native.NTSTATUS NtCreateThreadEx(ref IntPtr threadHandle,
        Data.Win32.WinNT.ACCESS_MASK desiredAccess, IntPtr objectAttributes, IntPtr processHandle, IntPtr startAddress,
        IntPtr parameter, bool createSuspended, int stackZeroBits, int sizeOfStack, int maximumStackSize,
        IntPtr attributeList)
    {
        object[] parameters = new object[] {             
            threadHandle, desiredAccess, objectAttributes, processHandle, startAddress, parameter, createSuspended,
            stackZeroBits,
            sizeOfStack, maximumStackSize, attributeList};

        var status = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL,
            "NtCreateThreadEx",
            typeof(NtCreateThreadExDelegate),
            ref parameters);

        threadHandle = (IntPtr)parameters[0];
        return status;
    }

    public static Data.Native.NTSTATUS NtCreateSection(ref IntPtr sectionHandle, uint desiredAccess,
        IntPtr objectAttributes, ref ulong maximumSize, uint sectionPageProtection, uint allocationAttributes,
        IntPtr fileHandle)
    {
        object[] parameters = new object[] {            
            sectionHandle, desiredAccess, objectAttributes, maximumSize,
            sectionPageProtection, allocationAttributes, fileHandle 
        };

        var status = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL,
            "NtCreateSection",
            typeof(NtCreateSectionDelegate),
            ref parameters);

        sectionHandle = (IntPtr)parameters[0];
        maximumSize = (ulong)parameters[3];

        return status;
    }

    public static Data.Native.NTSTATUS NtUnmapViewOfSection(IntPtr hProc, IntPtr baseAddr)
    {
        object[] parameters = new object[]{ hProc, baseAddr };

        return Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL, 
            "NtUnmapViewOfSection",
            typeof(NtUnmapViewOfSectionDelegate),
            ref parameters);
    }

    public static Data.Native.NTSTATUS NtMapViewOfSection(IntPtr sectionHandle, IntPtr processHandle,
        ref IntPtr baseAddress, IntPtr zeroBits, IntPtr commitSize, IntPtr sectionOffset, ref ulong viewSize,
        uint inheritDisposition, uint allocationType, uint win32Protect)
    {
        object[] parameters = new object[] {

            sectionHandle, processHandle, baseAddress, zeroBits, commitSize, sectionOffset, viewSize,
            inheritDisposition, allocationType, win32Protect
        };

        var status = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            "ntdll.dll", 
            "NtMapViewOfSection",
            typeof(NtMapViewOfSectionDelegate), 
            ref parameters);

        baseAddress = (IntPtr)parameters[2];
        viewSize = (ulong)parameters[6];

        return status;
    }

    public static void RtlInitUnicodeString(ref Data.Native.UNICODE_STRING destinationString,
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString)
    {
        object[] parameters = new object[] { destinationString, sourceString };

        Generic.DynamicApiInvoke<object>(
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

        var status = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
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

        _ = Generic.DynamicApiInvoke<object>(
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

        var status = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL, 
            "NtQueryInformationProcess",
            typeof(NtQueryInformationProcessDelegate), 
            ref parameters);

        pProcInfo = (IntPtr)parameters[2];
        return status;
    }

    public static bool NtQueryInformationProcessWow64Information(IntPtr hProcess)
    {
        _ = NtQueryInformationProcess(
            hProcess,
            Data.Native.PROCESSINFOCLASS.ProcessWow64Information,
            out var pProcInfo);

        return Marshal.ReadIntPtr(pProcInfo) != IntPtr.Zero;
    }

    public static Data.Native.PROCESS_BASIC_INFORMATION NtQueryInformationProcessBasicInformation(IntPtr hProcess)
    {
        _ = NtQueryInformationProcess(
            hProcess, 
            Data.Native.PROCESSINFOCLASS.ProcessBasicInformation,
            out var pProcInfo);

        return Marshal.PtrToStructure<Data.Native.PROCESS_BASIC_INFORMATION>(pProcInfo);
    }

    public static IntPtr NtAllocateVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, IntPtr zeroBits,
        ref IntPtr regionSize, uint allocationType, uint protect)
    {
        object[] parameters = new object[] { processHandle, baseAddress, zeroBits, regionSize, allocationType, protect };

        _ = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL,
            "NtAllocateVirtualMemory",
            typeof(NtAllocateVirtualMemoryDelegate),
            ref parameters);

        baseAddress = (IntPtr)parameters[1];
        return baseAddress;
    }

    public static void NtFreeVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, ref IntPtr regionSize,
        uint freeType)
    {
        object[] parameters = new object[] { processHandle, baseAddress, regionSize, freeType };

        _ = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL, 
            "NtFreeVirtualMemory",
            typeof(NtFreeVirtualMemoryDelegate),
            ref parameters);
    }

    public static uint NtProtectVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, ref IntPtr regionSize,
        uint newProtect)
    {
        object[] parameters = new object[] { processHandle, baseAddress, regionSize, newProtect, (uint)0 };

        _ = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL, 
            "NtProtectVirtualMemory",
            typeof(NtProtectVirtualMemoryDelegate),
            ref parameters);

        return (uint)parameters[4];
    }

    public static uint NtWriteVirtualMemory(IntPtr processHandle, IntPtr baseAddress, IntPtr buffer, uint bufferLength)
    {
        object[] parameters = new object[] { processHandle, baseAddress, buffer, bufferLength, (uint)0 };

        _ = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL, 
            "NtWriteVirtualMemory",
            typeof(NtWriteVirtualMemoryDelegate),
            ref parameters);

        return (uint)parameters[4];
    }

    public static IntPtr LdrGetProcedureAddress(IntPtr hModule, IntPtr functionName, IntPtr ordinal,
        ref IntPtr functionAddress)
    {
        object[] parameters = new object[] { hModule, functionName, ordinal, functionAddress };

        _ = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL, 
            "LdrGetProcedureAddress",
            typeof(LdrGetProcedureAddressDelegate),
            ref parameters);

        functionAddress = (IntPtr)parameters[3];
        return functionAddress;
    }

    public static void RtlGetVersion(ref Data.Native.OSVERSIONINFOEX versionInformation)
    {
        object[] parameters = new object[] { versionInformation };

        _ = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            NTDLL, 
            "RtlGetVersion",
            typeof(RtlGetVersionDelegate),
            ref parameters);

        versionInformation = (Data.Native.OSVERSIONINFOEX)parameters[0];
    }

    public static IntPtr NtOpenFile(ref IntPtr fileHandle, Data.Win32.Kernel32.FileAccessFlags desiredAccess,
        ref Data.Native.OBJECT_ATTRIBUTES objectAttributes, ref Data.Native.IO_STATUS_BLOCK ioStatusBlock,
        Data.Win32.Kernel32.FileShareFlags shareAccess, Data.Win32.Kernel32.FileOpenFlags openOptions)
    {
        object[] parameters = new object[] {
            fileHandle, desiredAccess, objectAttributes, ioStatusBlock, shareAccess, openOptions
        };

        _ = Generic.DynamicApiInvoke<Data.Native.NTSTATUS>(
            @"ntdll.dll",
            @"NtOpenFile",
            typeof(NtOpenFileDelegate),
            ref parameters);

        fileHandle = (IntPtr)parameters[0];
        return fileHandle;
    }

    public static void NtClose(IntPtr handle)
    {
        object[] parameters = new object[] { handle };

        _ = Generic.DynamicApiInvoke<uint>(
            NTDLL,
            "NtClose",
            typeof(NtCloseDelegate),
            ref parameters);
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