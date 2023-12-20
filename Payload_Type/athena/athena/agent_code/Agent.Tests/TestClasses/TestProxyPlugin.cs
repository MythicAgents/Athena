using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestProxyPlugin : IProxyPlugin
    {
        public Task HandleDatagram(ServerDatagram sm)
        {
            throw new NotImplementedException();
        }
    }
}
