using Agent.Models;
using Microsoft.Win32.SafeHandles;

namespace Agent.Interfaces
{
    public interface ISpawner
    {
        public abstract Task<bool> Spawn(SpawnOptions opts);
        public abstract bool TryGetHandle(string task_id, out SafeProcessHandle? handle);
    }
}
