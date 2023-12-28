using Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestInterfaces
{
    internal class TestAssemblyManager : IAssemblyManager
    {
        public bool LoadAssemblyAsync(string task_id, byte[] buf)
        {
            throw new NotImplementedException();
        }

        public bool LoadPluginAsync(string task_id, string pluginName, byte[] buf)
        {
            throw new NotImplementedException();
        }

        public bool TryGetPlugin(string name, out IPlugin? plugin)
        {
            throw new NotImplementedException();
        }

        public bool TryGetPlugin(string name, out IFilePlugin? plugin)
        {
            throw new NotImplementedException();
        }

        public bool TryGetPlugin(string name, out IProxyPlugin? plugin)
        {
            throw new NotImplementedException();
        }

        public bool TryGetPlugin(string name, out IForwarderPlugin? plugin)
        {
            throw new NotImplementedException();
        }

        public bool TryGetPlugin<T>(string name, out T? plugin) where T : IPlugin
        {
            throw new NotImplementedException();
        }
    }
}
