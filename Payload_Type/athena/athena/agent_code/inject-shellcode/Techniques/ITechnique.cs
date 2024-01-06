using System.Diagnostics;

namespace Agent
{
    internal interface ITechnique
    {
        internal bool Inject(byte[] shellcode, IntPtr hTarget);
        internal bool Inject(byte[] shellcode, Process proc);
    }
}
