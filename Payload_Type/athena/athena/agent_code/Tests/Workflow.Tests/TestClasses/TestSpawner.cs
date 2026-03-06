using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Tests.TestClasses
{
    public class TestSpawner : IRuntimeExecutor
    {
        public Task<bool> Spawn(SpawnOptions opts)
        {
            throw new NotImplementedException();
        }

        public bool TryGetHandle(string task_id, out SafeProcessHandle? handle)
        {
            throw new NotImplementedException();
        }
    }
}
