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
            return;
        }

        public async Task AddResponse(ResponseResult res)
        {
            taskResponses.Add(res.user_output); ;
            return;
        }

        public async Task AddResponse(FileBrowserResponseResult res)
        {
            taskResponses.Add(res.user_output);
            return;
        }

        public async Task AddResponse(ProcessResponseResult res)
        {
            taskResponses.Add(res.user_output);
            return;
        }

        public async Task AddResponse(DelegateMessage dm)
        {
            return;
        }

        public async Task AddResponse(DatagramSource source, ServerDatagram dg)
        {
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
            taskResponses.Add(output);
            return;
        }

        public async Task Write(string? output, string task_id, bool completed)
        {
            taskResponses.Add(output);
            return;
        }

        public async Task WriteLine(string? output, string task_id, bool completed, string status)
        {
            taskResponses.Add(output + Environment.NewLine);
            return;
        }

        public async Task WriteLine(string? output, string task_id, bool completed)
        {
            taskResponses.Add(output + Environment.NewLine);
            return;
        }

        public async Task<string> GetRecentOutput()
        {
            return taskResponses.FirstOrDefault();
        }

        public Task AddResponse(InteractMessage im)
        {
            throw new NotImplementedException();
        }
    }
}
