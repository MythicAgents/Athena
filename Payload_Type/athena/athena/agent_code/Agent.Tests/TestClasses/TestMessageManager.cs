using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.TestClasses
{
    internal class TestMessageManager : IMessageManager
    {
        public List<string> taskResponses = new List<string>();
        public Dictionary<string, ServerJob> activeJobs = new Dictionary<string, ServerJob>();
        public AutoResetEvent hasResponse = new AutoResetEvent(false);
        public void AddJob(ServerJob job)
        {
            return;
        }

        public async Task AddKeystroke(string window_title, string task_id, string key)
        {
            return;
        }

        public async Task AddResponse(string res)
        {
            taskResponses.Add(res);
            hasResponse.Set();
            return;
        }

        public async Task AddResponse(TaskResponse res)
        {
            taskResponses.Add(res.ToJson());
            hasResponse.Set();
            return;
        }

        public async Task AddResponse(FileBrowserTaskResponse res)
        {
            taskResponses.Add(res.ToJson());
            hasResponse.Set();
            return;
        }

        public async Task AddResponse(ProcessTaskResponse res)
        {
            taskResponses.Add(res.ToJson());
            hasResponse.Set();
            return;
        }

        public async Task AddResponse(DelegateMessage dm)
        {
            hasResponse.Set();
            return;
        }

        public async Task AddResponse(DatagramSource source, ServerDatagram dg)
        {
            hasResponse.Set();
            return;
        }

        public bool CaptureStdOut(string task_id)
        {
            return true;
        }

        public void CompleteJob(string task_id)
        {
            return;
        }

        public async Task<string> GetAgentResponseStringAsync()
        {
            return String.Empty;
        }

        public Dictionary<string, ServerJob> GetJobs()
        {
            return new Dictionary<string, ServerJob>();
        }

        public bool HasResponses()
        {
            return true;
        }

        public bool ReleaseStdOut()
        {
            return true;
        }

        public bool StdIsBusy()
        {
            return false;
        }

        public bool TryGetJob(string task_id, out ServerJob job)
        {
            job = null;
            return true;
        }

        public async Task Write(string? output, string task_id, bool completed, string status)
        {
            TaskResponse rr = new TaskResponse()
            {
                task_id = task_id,
                completed = completed,
                status = status,
                user_output = output,
            };

            taskResponses.Add(rr.ToJson());
            hasResponse.Set();
            return;
        }

        public async Task Write(string? output, string task_id, bool completed)
        {
            TaskResponse rr = new TaskResponse()
            {
                task_id = task_id,
                completed = completed,
                status = "",
                user_output = output
            };

            taskResponses.Add(rr.ToJson());
            hasResponse.Set();
            return;
        }

        public async Task WriteLine(string? output, string task_id, bool completed, string status)
        {
            TaskResponse rr = new TaskResponse()
            {
                task_id = task_id,
                completed = completed,
                status = "",
                user_output = output + Environment.NewLine,
            };

            taskResponses.Add(rr.ToJson());
            hasResponse.Set();
            return;
        }

        public async Task WriteLine(string? output, string task_id, bool completed)
        {
            TaskResponse rr = new TaskResponse()
            {
                task_id = task_id,
                completed = completed,
                status = "",
                user_output = output + Environment.NewLine,
            };

            taskResponses.Add(rr.ToJson());
            hasResponse.Set();
            return;
        }

        public async Task<string> GetRecentOutput()
        {
            return taskResponses.LastOrDefault();
        }

        public Task AddResponse(InteractMessage im)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetStdOut()
        {
            throw new NotImplementedException();
        }
    }
}
