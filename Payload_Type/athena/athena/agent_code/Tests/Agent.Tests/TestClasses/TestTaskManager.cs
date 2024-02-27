using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public async Task HandleInteractiveResponses(List<InteractMessage> responses)
        {
            return;
        }

        public async Task HandleProxyResponses(string type, List<ServerDatagram> responses)
        {
            return;
        }

        public async Task HandleServerResponses(List<ServerTaskingResponse> responses)
        {
            return;
        }

        public async Task StartTaskAsync(ServerJob job)
        {
            jobs.Add(job);
            return;
        }

        public void WaitForNumberOfJobs(int numberOfJobs)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            while (timer.Elapsed.TotalSeconds < 30 && jobs.Count < numberOfJobs) { };
            timer.Stop();
        }
    }
}
