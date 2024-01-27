using System.Diagnostics;

namespace Agent
{
    internal interface ITechnique
    {
        internal int id { get; }
        internal bool resolved { get; set; }
        internal bool Resolve();
        internal bool Inject(byte[] shellcode, IntPtr hTarget);
        internal bool Inject(byte[] shellcode, Process proc);
    }
}
