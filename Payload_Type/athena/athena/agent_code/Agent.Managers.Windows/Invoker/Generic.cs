// Author: Ryan Cobb (@cobbr_io)
// Project: SharpSploit (https://github.com/cobbr/SharpSploit)
// License: BSD 3-Clause

using Invoker.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Invoker.Dynamic;

/// <summary>
/// Generic is a class for dynamically invoking arbitrary API calls from memory or disk. DynamicInvoke avoids suspicious
/// P/Invoke signatures, imports, and IAT entries by loading modules and invoking their functions at runtime.
/// </summary>
public static class Generic
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ReadGs();

    private static readonly byte[] X64SyscallStub = new byte[] 
    {
        0x49, 0x89, 0xCA,                // mov r10, rcx
        0xB8, 0x00, 0x00, 0x00, 0x00,    // mov eax, ssn
        0x0F, 0x05,                      // syscall
        0xC3                             // ret
    };
    
    /// <summary>
    /// Dynamically invoke an arbitrary function from a DLL, providing its name, function prototype, and arguments.
    /// </summary>
    /// <author>The Wover (@TheRealWover)</author>
    /// <param name="dllName">Name of the DLL.</param>
    /// <param name="functionName">Name of the function.</param>
    /// <param name="functionDelegateType">Prototype for the function, represented as a Delegate object.</param>
    /// <param name="parameters">Parameters to pass to the function. Can be modified if function uses call by reference.</param>
    /// <param name="canLoadFromDisk">Whether the DLL may be loaded from disk if it is not already loaded. Default is false.</param>
    /// <param name="resolveForwards">Whether or not to resolve export forwards. Default is true.</param>
    /// <returns>Object returned by the function. Must be unmarshalled by the caller.</returns>
    public static T InvokeApi<T>(string dllName, string functionName, Type functionDelegateType, ref object[] parameters, bool canLoadFromDisk = false, bool resolveForwards = true)
    {
        var pFunction = GetLibraryAddress(dllName, functionName, canLoadFromDisk, resolveForwards);
        return InvokeFunc<T>(pFunction, functionDelegateType, ref parameters);
    }

    /// <summary>
    /// Dynamically invokes an arbitrary function from a pointer. Useful for manually mapped modules or loading/invoking unmanaged code from memory.
    /// </summary>
    /// <author>The Wover (@TheRealWover)</author>
    /// <param name="functionPointer">A pointer to the unmanaged function.</param>
    /// <param name="functionDelegateType">Prototype for the function, represented as a Delegate object.</param>
    /// <param name="parameters">Arbitrary set of parameters to pass to the function. Can be modified if function uses call by reference.</param>
    /// <returns>Object returned by the function. Must be unmarshalled by the caller.</returns>
    public static T InvokeFunc<T>(IntPtr functionPointer, Type functionDelegateType, ref object[] parameters)
    {
        var funcDelegate = Marshal.GetDelegateForFunctionPointer(functionPointer, functionDelegateType);
        return (T)funcDelegate.DynamicInvoke(parameters);
    }

    public static T InvokeAsm<T>(byte[] asmStub, Type functionDelegateType, ref object[] parameters)
    {
        unsafe
        {
            fixed (byte* buffer = asmStub)
            {
                var ptr = (IntPtr)buffer;
                var size = new IntPtr(asmStub.Length);

                var oldProtect = Native.NtProtectVirtualMemory(new IntPtr(-1), ref ptr,
                    ref size, Data.Win32.WinNT.PAGE_EXECUTE_READWRITE);

                var result = InvokeFunc<T>(ptr, functionDelegateType, ref parameters);

                Native.NtProtectVirtualMemory(new IntPtr(-1), ref ptr,
                    ref size, oldProtect);

                return result;
            }
        }
    }

    /// <summary>
    /// Resolves LdrLoadDll and uses that function to load a DLL from disk.
    /// </summary>
    /// <author>Ruben Boonen (@FuzzySec)</author>
    /// <param name="dllPath">The path to the DLL on disk. Uses the LoadLibrary convention.</param>
    /// <returns>IntPtr base address of the loaded module or IntPtr.Zero if the module was not loaded successfully.</returns>
    public static IntPtr LoadModuleFromDisk(string dllPath)
    {
        var uModuleName = new Data.Native.UNICODE_STRING();
        Native.RtlInitUnicodeString(ref uModuleName, dllPath);

        var hModule = IntPtr.Zero;
        var callResult = Native.LdrLoadDll(IntPtr.Zero, 0, ref uModuleName, ref hModule);
            
        if (callResult != Data.Native.NTSTATUS.Success || hModule == IntPtr.Zero)
            return IntPtr.Zero;

        return hModule;
    }

    /// <summary>
    /// Helper for getting the pointer to a function from a DLL loaded by the process.
    /// </summary>
    /// <author>Ruben Boonen (@FuzzySec)</author>
    /// <param name="dllName">The name of the DLL (e.g. "ntdll.dll" or "C:\Windows\System32\ntdll.dll").</param>
    /// <param name="functionName">Name of the exported procedure.</param>
    /// <param name="canLoadFromDisk">Optional, indicates if the function can try to load the DLL from disk if it is not found in the loaded module list.</param>
    /// <param name="resolveForwards">Whether or not to resolve export forwards. Default is true.</param>
    /// <returns>IntPtr for the desired function.</returns>
    public static IntPtr GetLibraryAddress(string dllName, string functionName, bool canLoadFromDisk = false, bool resolveForwards = true)
    {
        var hModule = GetLoadedModulePtr(dllName);
            
        if (hModule == IntPtr.Zero && canLoadFromDisk)
        {
            hModule = LoadModuleFromDisk(dllName);
                
            if (hModule == IntPtr.Zero)
                throw new FileNotFoundException(dllName + ", unable to find the specified file.");
        }
        else if (hModule == IntPtr.Zero)
        {
            throw new DllNotFoundException(dllName + ", Dll was not found.");
        }

        return GetExportAddr(hModule, functionName, resolveForwards);
    }

    /// <summary>
    /// Helper for getting the base address of a module loaded by the current process. This base
    /// address could be passed to GetProcAddress/LdrGetProcedureAddress or it could be used for
    /// manual export parsing. This function uses the .NET System.Diagnostics.Process class.
    /// </summary>
    /// <author>Ruben Boonen (@FuzzySec)</author>
    /// <param name="dllName">The name of the DLL (e.g. "ntdll.dll").</param>
    /// <returns>IntPtr base address of the loaded module or IntPtr.Zero if the module is not found.</returns>
    public static IntPtr GetLoadedModulePtr(string dllName)
    {
        using var process = Process.GetCurrentProcess();

        foreach (ProcessModule module in process.Modules)
        {
            if (module.ModuleName.Equals(dllName, StringComparison.OrdinalIgnoreCase))
                return module.BaseAddress;
        }
            
        return IntPtr.Zero;
    }

    /// <summary>
    /// Helper for getting the base address of a module loaded by the current process. This base
    /// address could be passed to GetProcAddress/LdrGetProcedureAddress or it could be used for
    /// manual export parsing. This function uses the .NET System.Diagnostics.Process class.
    /// </summary>
    /// <param name="hashedDllName">Hash of the DLL name.</param>
    /// <param name="key">64-bit integer to initialize the keyed hash object (e.g. 0xabc or 0x1122334455667788).</param>
    /// <returns>IntPtr base address of the loaded module or IntPtr.Zero if the module is not found.</returns>
    public static IntPtr GetLoadedModulePtr(string hashedDllName, long key)
    {
        using var process = Process.GetCurrentProcess();

        foreach (ProcessModule module in process.Modules)
        {
            var hashedName = Utilities.GetFuncHash(module.ModuleName, key);
            
            if (hashedName.Equals(hashedDllName))
                return module.BaseAddress;
        }
        
        return IntPtr.Zero;
    }

    /// <summary>
    /// This function uses dynamic assembly invocation to obtain a pointer to the PEB.
    /// __readgsqword(0x60) or __readfsdword(0x30)
    /// </summary>
    /// <returns>Base address of the PEB as an IntPtr.</returns>
    public static IntPtr GetPebAddress()
    {
        byte[] stub;
        
        if (IntPtr.Size == 8)
        {
            stub = new byte[] 
            {

                0x65, 0x48, 0x8B, 0x04, 0x25, 0x60,     // mov rax, qword ptr gs:[0x60]
                0x00, 0x00, 0x00,
                0xc3                                    // ret
            };
        }
        else
        {
            stub = new byte[]
            {
                0x64, 0xA1, 0x30, 0x00, 0x00, 0x00,     // mov eax,dword ptr fs:[30]
                0xC3                                    // ret
            };
        }

        var parameters = Array.Empty<object>();
        
        return InvokeAsm<IntPtr>(
            stub,
            typeof(ReadGs),
            ref parameters);
    }

    /// <summary>
    /// Helper for getting the base address of a module loaded by the current process. This base
    /// address could be passed to GetProcAddress/LdrGetProcedureAddress or it could be used for
    /// manual export parsing. This function parses the _PEB_LDR_DATA structure.
    /// </summary>
    /// <author>Ruben Boonen (@FuzzySec)</author>
    /// <param name="dllName">The name of the DLL (e.g. "ntdll.dll").</param>
    /// <returns>IntPtr base address of the loaded module or IntPtr.Zero if the module is not found.</returns>
    public static IntPtr GetPebLdrModEntry(string dllName)
    {
        // Set function variables
        uint ldrDataOffset;
        uint inLoadOrderModuleListOffset;
            
        if (IntPtr.Size == 4)
        {
            ldrDataOffset = 0xc;
            inLoadOrderModuleListOffset = 0xC;
        }
        else
        {
            ldrDataOffset = 0x18;
            inLoadOrderModuleListOffset = 0x10;
        }

        // Get _PEB pointer
        var pPeb = GetPebAddress();

        // Get module InLoadOrderModuleList -> _LIST_ENTRY
        var pebLdrData = Marshal.ReadIntPtr((IntPtr)((ulong)pPeb + ldrDataOffset));
        var pInLoadOrderModuleList = (IntPtr)((ulong)pebLdrData + inLoadOrderModuleListOffset);
        var le = (Data.Native.LIST_ENTRY)Marshal.PtrToStructure(pInLoadOrderModuleList, typeof(Data.Native.LIST_ENTRY));

        // Loop entries
        var flink = le.Flink;
        var hModule = IntPtr.Zero;
        var dte = (Data.PE.LDR_DATA_TABLE_ENTRY)Marshal.PtrToStructure(flink, typeof(Data.PE.LDR_DATA_TABLE_ENTRY));
        while (dte.InLoadOrderLinks.Flink != le.Blink)
        {
            // Match module name
            var moduleName = Marshal.PtrToStringUni(dte.BaseDllName.Buffer);
            if (!string.IsNullOrWhiteSpace(moduleName) && moduleName.Equals(dllName, StringComparison.OrdinalIgnoreCase))
            {
                hModule = dte.DllBase;
                break;
            }

            // Move Ptr
            flink = dte.InLoadOrderLinks.Flink;
            dte = (Data.PE.LDR_DATA_TABLE_ENTRY)Marshal.PtrToStructure(flink, typeof(Data.PE.LDR_DATA_TABLE_ENTRY));
        }

        return hModule;
    }

    /// <summary>
    /// Given a module base address, resolve the address of a function by manually walking the module export table.
    /// </summary>
    /// <author>Ruben Boonen (@FuzzySec)</author>
    /// <param name="moduleBase">A pointer to the base address where the module is loaded in the current process.</param>
    /// <param name="exportName">The name of the export to search for (e.g. "NtAlertResumeThread").</param>
    /// <param name="resolveForwards">Whether or not to resolve export forwards. Default is true.</param>
    /// <returns>IntPtr for the desired function.</returns>
    public static IntPtr GetExportAddr(IntPtr moduleBase, string exportName, bool resolveForwards = true)
    {
        var functionPtr = IntPtr.Zero;
            
        try
        {
            // Traverse the PE header in memory
            var peHeader = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + 0x3C));
            var optHeader = moduleBase.ToInt64() + peHeader + 0x18;
            var magic = Marshal.ReadInt16((IntPtr)optHeader);
            long pExport;
                
            if (magic == 0x010b) pExport = optHeader + 0x60;
            else pExport = optHeader + 0x70;

            var exportRva = Marshal.ReadInt32((IntPtr)pExport);
            var ordinalBase = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x10));
            var numberOfNames = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x18));
            var functionsRva = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x1C));
            var namesRva = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x20));
            var ordinalsRva = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x24));
                
            for (var i = 0; i < numberOfNames; i++)
            {
                var functionName = Marshal.PtrToStringAnsi((IntPtr)(moduleBase.ToInt64() + Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + namesRva + i * 4))));
                if (string.IsNullOrWhiteSpace(functionName)) continue;
                if (!functionName.Equals(exportName, StringComparison.OrdinalIgnoreCase)) continue;
                    
                var functionOrdinal = Marshal.ReadInt16((IntPtr)(moduleBase.ToInt64() + ordinalsRva + i * 2)) + ordinalBase;
                    
                var functionRva = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + functionsRva + 4 * (functionOrdinal - ordinalBase)));
                functionPtr = (IntPtr)((long)moduleBase + functionRva);
                        
                if (resolveForwards)
                    functionPtr = GetForwardAddress(functionPtr);

                break;
            }
        }
        catch
        {
            throw new InvalidOperationException("Failed to parse module exports.");
        }

        if (functionPtr == IntPtr.Zero)
            throw new MissingMethodException(exportName + ", export not found.");

        return functionPtr;
    }

    /// <summary>
    /// Given a module base address, resolve the address of a function by manually walking the module export table.
    /// </summary>
    /// <author>Ruben Boonen (@FuzzySec)</author>
    /// <param name="moduleBase">A pointer to the base address where the module is loaded in the current process.</param>
    /// <param name="functionHash">Hash of the exported procedure.</param>
    /// <param name="key">64-bit integer to initialize the keyed hash object (e.g. 0xabc or 0x1122334455667788).</param>
    /// <param name="resolveForwards">Whether or not to resolve export forwards. Default is true.</param>
    /// <returns>IntPtr for the desired function.</returns>
    public static IntPtr GetExportAddr(IntPtr moduleBase, string functionHash, long key, bool resolveForwards = true)
    {
        var functionPtr = IntPtr.Zero;
            
        try
        {
            var peHeader = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + 0x3C));
            var optHeader = moduleBase.ToInt64() + peHeader + 0x18;
            var magic = Marshal.ReadInt16((IntPtr)optHeader);
            long pExport;
                
            if (magic == 0x010b) pExport = optHeader + 0x60;
            else pExport = optHeader + 0x70;

            var exportRva = Marshal.ReadInt32((IntPtr)pExport);
            var ordinalBase = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x10));
            var numberOfNames = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x18));
            var functionsRva = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x1C));
            var namesRva = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x20));
            var ordinalsRva = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + exportRva + 0x24));

            for (var i = 0; i < numberOfNames; i++)
            {
                var functionName = Marshal.PtrToStringAnsi((IntPtr)(moduleBase.ToInt64() + Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + namesRva + i * 4))));
                if (string.IsNullOrWhiteSpace(functionName)) continue;
                if (!Utilities.GetFuncHash(functionName, key).Equals(functionHash, StringComparison.OrdinalIgnoreCase)) continue;
                    
                var functionOrdinal = Marshal.ReadInt16((IntPtr)(moduleBase.ToInt64() + ordinalsRva + i * 2)) + ordinalBase;
                    
                var functionRva = Marshal.ReadInt32((IntPtr)(moduleBase.ToInt64() + functionsRva + 4 * (functionOrdinal - ordinalBase)));
                functionPtr = (IntPtr)((long)moduleBase + functionRva);

                if (resolveForwards)
                    functionPtr = GetForwardAddress(functionPtr);

                break;
            }
        }
        catch
        {
            throw new InvalidOperationException("Failed to parse module exports.");
        }

        if (functionPtr == IntPtr.Zero)
            throw new MissingMethodException(functionHash + ", export hash not found.");
            
        return functionPtr;
    }

    /// <summary>
    /// Check if an address to an exported function should be resolved to a forward. If so, return the address of the forward.
    /// </summary>
    /// <author>The Wover (@TheRealWover)</author>
    /// <param name="exportAddress">Function of an exported address, found by parsing a PE file's export table.</param>
    /// <param name="canLoadFromDisk">Optional, indicates if the function can try to load the DLL from disk if it is not found in the loaded module list.</param>
    /// <returns>IntPtr for the forward. If the function is not forwarded, return the original pointer.</returns>
    public static IntPtr GetForwardAddress(IntPtr exportAddress, bool canLoadFromDisk = false)
    {
        var functionPtr = exportAddress;
            
        try
        {
            var forwardNames = Marshal.PtrToStringAnsi(functionPtr);
            if (string.IsNullOrWhiteSpace(forwardNames)) return functionPtr;
                
            var values = forwardNames.Split('.');

            if (values.Length > 1)
            {
                var forwardModuleName = values[0];
                var forwardExportName = values[1];

                var apiSet = GetApiSetMapping();
                var lookupKey = forwardModuleName.Substring(0, forwardModuleName.Length - 2) + ".dll";
                    
                if (apiSet.ContainsKey(lookupKey))
                    forwardModuleName = apiSet[lookupKey];
                else
                    forwardModuleName = forwardModuleName + ".dll";

                var hModule = GetPebLdrModEntry(forwardModuleName);
                    
                if (hModule == IntPtr.Zero && canLoadFromDisk)
                    hModule = LoadModuleFromDisk(forwardModuleName);
                    
                if (hModule != IntPtr.Zero)
                    functionPtr = GetExportAddr(hModule, forwardExportName);
            }
        }
        catch
        {
            // Do nothing, it was not a forward
        }
            
        return functionPtr;
    }

    /// <summary>
    /// Resolve host DLL for API Set DLL.
    /// </summary>
    /// <author>Ruben Boonen (@FuzzySec), The Wover (@TheRealWover)</author>
    /// <returns>Dictionary, a combination of Key:APISetDLL and Val:HostDLL.</returns>
    public static Dictionary<string, string> GetApiSetMapping()
    {
        var pbi = Native.NtQueryInformationProcessBasicInformation((IntPtr)(-1));
        var apiSetMapOffset = IntPtr.Size == 4 ? (uint)0x38 : 0x68;
        var apiSetDict = new Dictionary<string, string>();

        var pApiSetNamespace = Marshal.ReadIntPtr((IntPtr)((ulong)pbi.PebBaseAddress + apiSetMapOffset));
        var apiSetNamespace = (Data.PE.ApiSetNamespace)Marshal.PtrToStructure(pApiSetNamespace, typeof(Data.PE.ApiSetNamespace));
            
        for (var i = 0; i < apiSetNamespace.Count; i++)
        {
            var setEntry = new Data.PE.ApiSetNamespaceEntry();

            var pSetEntry = (IntPtr)((ulong)pApiSetNamespace + (ulong)apiSetNamespace.EntryOffset + (ulong)(i * Marshal.SizeOf(setEntry)));
            setEntry = (Data.PE.ApiSetNamespaceEntry)Marshal.PtrToStructure(pSetEntry, typeof(Data.PE.ApiSetNamespaceEntry));

            var apiSetEntryName = Marshal.PtrToStringUni((IntPtr)((ulong)pApiSetNamespace + (ulong)setEntry.NameOffset), setEntry.NameLength / 2);
            var apiSetEntryKey = apiSetEntryName.Substring(0, apiSetEntryName.Length - 2) + ".dll" ; // Remove the patch number and add .dll

            var valueEntry = new Data.PE.ApiSetValueEntry();
            var pSetValue = IntPtr.Zero;

            switch (setEntry.ValueLength)
            {
                case 1:
                    pSetValue = (IntPtr)((ulong)pApiSetNamespace + (ulong)setEntry.ValueOffset);
                    break;
                    
                case > 1:
                {
                    for (var j = 0; j < setEntry.ValueLength; j++)
                    {
                        var host = (IntPtr)((ulong)pApiSetNamespace + (ulong)setEntry.ValueOffset + (ulong)Marshal.SizeOf(valueEntry) * (ulong)j);
                        if (Marshal.PtrToStringUni(host) != apiSetEntryName)
                            pSetValue = (IntPtr)((ulong)pApiSetNamespace + (ulong)setEntry.ValueOffset + (ulong)Marshal.SizeOf(valueEntry) * (ulong)j);
                    }
                        
                    if (pSetValue == IntPtr.Zero)
                        pSetValue = (IntPtr)((ulong)pApiSetNamespace + (ulong)setEntry.ValueOffset);
                        
                    break;
                }
            }

            valueEntry = (Data.PE.ApiSetValueEntry)Marshal.PtrToStructure(pSetValue, typeof(Data.PE.ApiSetValueEntry));
                
            var apiSetValue = string.Empty;
            if (valueEntry.ValueCount != 0)
            {
                var pValue = (IntPtr)((ulong)pApiSetNamespace + (ulong)valueEntry.ValueOffset);
                apiSetValue = Marshal.PtrToStringUni(pValue, valueEntry.ValueCount / 2);
            }

            apiSetDict.Add(apiSetEntryKey, apiSetValue);
        }

        return apiSetDict;
    }
}