using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestTaskManager : ITaskManager
    {
        public List<ServerJob> jobs = new List<ServerJob>();
        public async Task HandleDelegateResponses(List<DelegateMessage> responses)
        {
            return;
        }

        public async Task HandleProxyResponses(string type, List<ServerDatagram> responses)
        {
            return;
        }

        public async Task HandleServerResponses(List<ServerResponseResult> responses)
        {
            return;
        }

        public async Task StartTaskAsync(ServerJob job)
        {
            jobs.Add(job);
            return;
        }
    }
}
