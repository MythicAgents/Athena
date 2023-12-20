using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestForwarderPlugin : IForwarderPlugin
    {
        public Task ForwardDelegate(DelegateMessage dm)
        {
            throw new NotImplementedException();
        }
    }
}
