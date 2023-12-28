using System.Diagnostics;

namespace Agent.Techniques
{
    internal interface ITechnique
    {
        internal bool Inject(byte[] shellcode, IntPtr hTarget);
        internal bool Inject(byte[] shellcode, Process proc);
    }
}
