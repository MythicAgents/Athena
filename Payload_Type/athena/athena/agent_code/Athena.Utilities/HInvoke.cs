using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Utilities
{
    public class HInvoke
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

        /// <summary>
        /// Credit to  @sbasu7241 for the following code: https://gist.github.com/sbasu7241/148a60d6831dc31e854f59f2951f7989
        /// </summary>
        public static IntPtr GetfuncaddressbyHash(string library, long hash)
        {
            IntPtr handle = LoadLibrary(library);

            STRUCTS.IMAGE_DOS_HEADER dosheader = (STRUCTS.IMAGE_DOS_HEADER)Marshal.PtrToStructure(handle, typeof(STRUCTS.IMAGE_DOS_HEADER));

            IntPtr sgn = IntPtr.Add(handle, (int)dosheader.e_lfanew);
            STRUCTS.SIGNATURE sign = (STRUCTS.SIGNATURE)Marshal.PtrToStructure(sgn, typeof(STRUCTS.SIGNATURE));

            int si = 4 * sizeof(byte);
            IntPtr file_head = IntPtr.Add(sgn, si);
            STRUCTS.IMAGE_FILE_HEADER fileheader = (STRUCTS.IMAGE_FILE_HEADER)Marshal.PtrToStructure(file_head, typeof(STRUCTS.IMAGE_FILE_HEADER));

            int ti;
            unsafe
            {
                ti = sizeof(STRUCTS.IMAGE_FILE_HEADER);
            }
            IntPtr opt_head = IntPtr.Add(file_head, ti);
            STRUCTS.IMAGE_OPTIONAL_HEADER64 optheader = (STRUCTS.IMAGE_OPTIONAL_HEADER64)Marshal.PtrToStructure(opt_head, typeof(STRUCTS.IMAGE_OPTIONAL_HEADER64));

            IntPtr export_directory = IntPtr.Add(handle, (int)optheader.ExportTable.VirtualAddress);

            STRUCTS.IMAGE_EXPORT_DIRECTORY export_header = (STRUCTS.IMAGE_EXPORT_DIRECTORY)Marshal.PtrToStructure(export_directory, typeof(STRUCTS.IMAGE_EXPORT_DIRECTORY));

            int no_of_names = (int)export_header.NumberOfNames;

            IntPtr address_functions = IntPtr.Add(handle, (int)export_header.AddressOfFunctions);
            IntPtr address_names = IntPtr.Add(handle, (int)export_header.AddressOfNames);

            IntPtr func_exact_address = IntPtr.Zero;
            string functionname = "";

            for (int i = 0; i < no_of_names; i++)
            {
                IntPtr func_name_address = IntPtr.Add(address_names, (sizeof(int)) * i);
                int function_name_rva = Marshal.ReadInt32(func_name_address);
                IntPtr func_name_string = IntPtr.Add(handle, function_name_rva);


                functionname = Marshal.PtrToStringAnsi(func_name_string);
                if (Gethashfromstring(functionname) == hash)
                {
                    IntPtr func_address = IntPtr.Add(address_functions, (sizeof(int)) * i);

                    int func_address_rva = Marshal.ReadInt32(func_address);

                    func_exact_address = IntPtr.Add(handle, func_address_rva);
                    break;

                }
            }
            return func_exact_address;
        }
        public static long Gethashfromstring(string inp)
        {
            long sm = 0;
            foreach (char c in inp)
            {
                sm = sm * 10 + ((int)c % 10);
            }
            return sm;
        }
        
        class STRUCTS
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct STARTUPINFO
            {
                public Int32 cb;
                public string lpReserved;
                public string lpDesktop;
                public string lpTitle;
                public Int32 dwX;
                public Int32 dwY;
                public Int32 dwXSize;
                public Int32 dwYSize;
                public Int32 dwXCountChars;
                public Int32 dwYCountChars;
                public Int32 dwFillAttribute;
                public Int32 dwFlags;
                public Int16 wShowWindow;
                public Int16 cbReserved2;
                public IntPtr lpReserved2;
                public IntPtr hStdInput;
                public IntPtr hStdOutput;
                public IntPtr hStdError;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESS_INFORMATION
            {
                public IntPtr hProcess;
                public IntPtr hThread;
                public int dwProcessId;
                public int dwThreadId;
            }

            [Flags]
            public enum ProcessCreationFlags : uint
            {
                ZERO_FLAG = 0x00000000,
                CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
                CREATE_DEFAULT_ERROR_MODE = 0x04000000,
                CREATE_NEW_CONSOLE = 0x00000010,
                CREATE_NEW_PROCESS_GROUP = 0x00000200,
                CREATE_NO_WINDOW = 0x08000000,
                CREATE_PROTECTED_PROCESS = 0x00040000,
                CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
                CREATE_SEPARATE_WOW_VDM = 0x00001000,
                CREATE_SHARED_WOW_VDM = 0x00001000,
                CREATE_SUSPENDED = 0x00000004,
                CREATE_UNICODE_ENVIRONMENT = 0x00000400,
                DEBUG_ONLY_THIS_PROCESS = 0x00000002,
                DEBUG_PROCESS = 0x00000001,
                DETACHED_PROCESS = 0x00000008,
                EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
                INHERIT_PARENT_AFFINITY = 0x00010000
            }

            public struct IMAGE_DOS_HEADER
            {      // DOS .EXE header
                public UInt16 e_magic;              // Magic number
                public UInt16 e_cblp;               // Bytes on last page of file
                public UInt16 e_cp;                 // Pages in file
                public UInt16 e_crlc;               // Relocations
                public UInt16 e_cparhdr;            // Size of header in paragraphs
                public UInt16 e_minalloc;           // Minimum extra paragraphs needed
                public UInt16 e_maxalloc;           // Maximum extra paragraphs needed
                public UInt16 e_ss;                 // Initial (relative) SS value
                public UInt16 e_sp;                 // Initial SP value
                public UInt16 e_csum;               // Checksum
                public UInt16 e_ip;                 // Initial IP value
                public UInt16 e_cs;                 // Initial (relative) CS value
                public UInt16 e_lfarlc;             // File address of relocation table
                public UInt16 e_ovno;               // Overlay number
                public UInt16 e_res_0;              // Reserved words
                public UInt16 e_res_1;              // Reserved words
                public UInt16 e_res_2;              // Reserved words
                public UInt16 e_res_3;              // Reserved words
                public UInt16 e_oemid;              // OEM identifier (for e_oeminfo)
                public UInt16 e_oeminfo;            // OEM information; e_oemid specific
                public UInt16 e_res2_0;             // Reserved words
                public UInt16 e_res2_1;             // Reserved words
                public UInt16 e_res2_2;             // Reserved words
                public UInt16 e_res2_3;             // Reserved words
                public UInt16 e_res2_4;             // Reserved words
                public UInt16 e_res2_5;             // Reserved words
                public UInt16 e_res2_6;             // Reserved words
                public UInt16 e_res2_7;             // Reserved words
                public UInt16 e_res2_8;             // Reserved words
                public UInt16 e_res2_9;             // Reserved words
                public UInt32 e_lfanew;             // File address of new exe header
            }
            public struct SIGNATURE
            {
                public UInt32 signature;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct IMAGE_FILE_HEADER
            {
                public UInt16 Machine;
                public UInt16 NumberOfSections;
                public UInt32 TimeDateStamp;
                public UInt32 PointerToSymbolTable;
                public UInt32 NumberOfSymbols;
                public UInt16 SizeOfOptionalHeader;
                public UInt16 Characteristics;
            }

            public struct IMAGE_OPTIONAL_HEADER64
            {
                public UInt16 Magic;
                public Byte MajorLinkerVersion;
                public Byte MinorLinkerVersion;
                public UInt32 SizeOfCode;
                public UInt32 SizeOfInitializedData;
                public UInt32 SizeOfUninitializedData;
                public UInt32 AddressOfEntryPoint;
                public UInt32 BaseOfCode;
                public UInt64 ImageBase;
                public UInt32 SectionAlignment;
                public UInt32 FileAlignment;
                public UInt16 MajorOperatingSystemVersion;
                public UInt16 MinorOperatingSystemVersion;
                public UInt16 MajorImageVersion;
                public UInt16 MinorImageVersion;
                public UInt16 MajorSubsystemVersion;
                public UInt16 MinorSubsystemVersion;
                public UInt32 Win32VersionValue;
                public UInt32 SizeOfImage;
                public UInt32 SizeOfHeaders;
                public UInt32 CheckSum;
                public UInt16 Subsystem;
                public UInt16 DllCharacteristics;
                public UInt64 SizeOfStackReserve;
                public UInt64 SizeOfStackCommit;
                public UInt64 SizeOfHeapReserve;
                public UInt64 SizeOfHeapCommit;
                public UInt32 LoaderFlags;
                public UInt32 NumberOfRvaAndSizes;

                public IMAGE_DATA_DIRECTORY ExportTable;
                public IMAGE_DATA_DIRECTORY ImportTable;
                public IMAGE_DATA_DIRECTORY ResourceTable;
                public IMAGE_DATA_DIRECTORY ExceptionTable;
                public IMAGE_DATA_DIRECTORY CertificateTable;
                public IMAGE_DATA_DIRECTORY BaseRelocationTable;
                public IMAGE_DATA_DIRECTORY Debug;
                public IMAGE_DATA_DIRECTORY Architecture;
                public IMAGE_DATA_DIRECTORY GlobalPtr;
                public IMAGE_DATA_DIRECTORY TLSTable;
                public IMAGE_DATA_DIRECTORY LoadConfigTable;
                public IMAGE_DATA_DIRECTORY BoundImport;
                public IMAGE_DATA_DIRECTORY IAT;
                public IMAGE_DATA_DIRECTORY DelayImportDescriptor;
                public IMAGE_DATA_DIRECTORY CLRRuntimeHeader;
                public IMAGE_DATA_DIRECTORY Reserved;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct IMAGE_DATA_DIRECTORY
            {
                public UInt32 VirtualAddress;
                public UInt32 Size;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct IMAGE_EXPORT_DIRECTORY
            {
                public UInt32 Characteristics;
                public UInt32 TimeDateStamp;
                public UInt16 MajorVersion;
                public UInt16 MinorVersion;
                public UInt32 Name;
                public UInt32 Base;
                public UInt32 NumberOfFunctions;
                public UInt32 NumberOfNames;
                public UInt32 AddressOfFunctions;     // RVA from base of image
                public UInt32 AddressOfNames;     // RVA from base of image
                public UInt32 AddressOfNameOrdinals;  // RVA from base of image
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate Boolean CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, STRUCTS.ProcessCreationFlags dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STRUCTS.STARTUPINFO lpStartupInfo, out STRUCTS.PROCESS_INFORMATION lpProcessInformation);

        }
    }
}
