using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestInterfaces
{
    internal class TestPlugin : IPlugin
    {
        public string Name => throw new NotImplementedException();

        public Task Execute(ServerJob job)
        {
            throw new NotImplementedException();
        }
    }
}
