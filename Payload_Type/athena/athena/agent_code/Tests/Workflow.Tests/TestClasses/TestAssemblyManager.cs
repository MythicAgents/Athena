using Workflow.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Tests.TestInterfaces
{
    internal class TestComponentProvider : IComponentProvider
    {
        public bool LoadAssemblyAsync(string task_id, byte[] buf)
        {
            throw new NotImplementedException();
        }

        public bool LoadModuleAsync(string task_id, string moduleName, byte[] buf)
        {
            throw new NotImplementedException();
        }

        public bool TryGetModule(string name, out IModule? plugin)
        {
            throw new NotImplementedException();
        }

        public bool TryGetModule(string name, out IFileModule? plugin)
        {
            throw new NotImplementedException();
        }

        public bool TryGetModule(string name, out IProxyModule? plugin)
        {
            throw new NotImplementedException();
        }

        public bool TryGetModule(string name, out IForwarderModule? plugin)
        {
            throw new NotImplementedException();
        }

        public bool TryGetModule<T>(string name, out T? plugin) where T : IModule
        {
            throw new NotImplementedException();
        }
    }
}
