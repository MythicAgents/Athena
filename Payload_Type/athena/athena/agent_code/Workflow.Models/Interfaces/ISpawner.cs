using Workflow.Models;
using Microsoft.Win32.SafeHandles;

namespace Workflow.Contracts
{
    public interface IRuntimeExecutor
    {
        public abstract Task<bool> Spawn(SpawnOptions opts);
        public abstract bool TryGetHandle(string task_id, out SafeProcessHandle? handle);
    }
}
