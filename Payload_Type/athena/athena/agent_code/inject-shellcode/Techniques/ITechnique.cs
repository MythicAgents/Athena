using System.Diagnostics;
using Workflow.Contracts;
using Workflow.Models;
namespace Workflow
{
    internal interface ITechnique
    {
        internal int id { get; }
        //internal bool Inject(byte[] shellcode, IntPtr hTarget);
        //internal bool Inject(byte[] shellcode, Process proc);\
        internal Task<bool> Inject(IRuntimeExecutor spawner, SpawnOptions spawnOptions, byte[] shellcode);
    }
}
