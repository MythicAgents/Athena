using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestForwarderPlugin : IForwarderPlugin
    {
        public string Name => throw new NotImplementedException();

        public Task Execute(ServerJob job)
        {
            throw new NotImplementedException();
        }

        public Task ForwardDelegate(DelegateMessage dm)
        {
            throw new NotImplementedException();
        }
    }
}
