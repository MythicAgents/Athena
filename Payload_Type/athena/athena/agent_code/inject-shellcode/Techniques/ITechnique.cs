using System.Diagnostics;
using Agent.Interfaces;
using Agent.Models;
namespace Agent
{
    internal interface ITechnique
    {
        internal int id { get; }
        //internal bool Inject(byte[] shellcode, IntPtr hTarget);
        //internal bool Inject(byte[] shellcode, Process proc);\
        internal Task<bool> Inject(ISpawner spawner, SpawnOptions spawnOptions, byte[] shellcode);
    }
}
