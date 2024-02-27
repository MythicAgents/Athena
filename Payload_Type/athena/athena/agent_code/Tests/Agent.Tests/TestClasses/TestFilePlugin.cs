using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestInterfaces
{
    internal class TestFilePlugin : IFilePlugin
    {
        public string Name => throw new NotImplementedException();

        public Task Execute(ServerJob job)
        {
            throw new NotImplementedException();
        }

        public Task HandleNextMessage(ServerTaskingResponse response)
        {
            throw new NotImplementedException();
        }
    }
}
