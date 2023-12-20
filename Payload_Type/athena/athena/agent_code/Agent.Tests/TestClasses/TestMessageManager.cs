using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestMessageManager : IMessageManager
    {
        public void AddJob(ServerJob job)
        {
            throw new NotImplementedException();
        }

        public Task AddKeystroke(string window_title, string task_id, string key)
        {
            throw new NotImplementedException();
        }

        public Task AddResponse(string res)
        {
            throw new NotImplementedException();
        }

        public Task AddResponse(ResponseResult res)
        {
            throw new NotImplementedException();
        }

        public Task AddResponse(FileBrowserResponseResult res)
        {
            throw new NotImplementedException();
        }

        public Task AddResponse(ProcessResponseResult res)
        {
            throw new NotImplementedException();
        }

        public Task AddResponse(DelegateMessage dm)
        {
            throw new NotImplementedException();
        }

        public Task AddResponse(DatagramSource source, ServerDatagram dg)
        {
            throw new NotImplementedException();
        }

        public bool CaptureStdOut(string task_id)
        {
            throw new NotImplementedException();
        }

        public void CompleteJob(string task_id)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetAgentResponseStringAsync()
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, ServerJob> GetJobs()
        {
            throw new NotImplementedException();
        }

        public bool HasResponses()
        {
            throw new NotImplementedException();
        }

        public bool ReleaseStdOut()
        {
            throw new NotImplementedException();
        }

        public bool StdIsBusy()
        {
            throw new NotImplementedException();
        }

        public bool TryGetJob(string task_id, out ServerJob job)
        {
            throw new NotImplementedException();
        }

        public Task Write(string? output, string task_id, bool completed, string status)
        {
            throw new NotImplementedException();
        }

        public Task Write(string? output, string task_id, bool completed)
        {
            throw new NotImplementedException();
        }

        public Task WriteLine(string? output, string task_id, bool completed, string status)
        {
            throw new NotImplementedException();
        }

        public Task WriteLine(string? output, string task_id, bool completed)
        {
            throw new NotImplementedException();
        }
    }
}
