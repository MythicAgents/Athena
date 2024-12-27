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

        public void AddDatagram(DatagramSource source, ServerDatagram dg)
        {
            hasResponse.Set();
            return;
        }

        public void AddDelegateMessage(DelegateMessage dm)
        {
            hasResponse.Set();
            return;
        }

        public void AddInteractMessage(InteractMessage im)
        {
            throw new NotImplementedException();
        }

        public void AddJob(ServerJob job)
        {
            throw new NotImplementedException();
        }

        public void AddKeystroke(string window_title, string task_id, string key)
        {
            throw new NotImplementedException();
        }

        public void AddTaskResponse(ITaskResponse res)
        {
            Console.WriteLine("Adding Task Response.");
            if (res is null)
            {
                throw new ArgumentNullException(nameof(res));
            }

            switch (res)
            {
                case FileBrowserTaskResponse filebrowserTaskResponse:
                    this.taskResponses.Add(filebrowserTaskResponse.ToJson());
                    break;
                case ProcessTaskResponse processTaskResponse:
                    this.taskResponses.Add(processTaskResponse.ToJson());
                    break;
                case TaskResponse taskResponse:
                    AddTaskResponse(taskResponse.ToJson());
                    break;
                default:
                    throw new ArgumentException($"Unsupported response type: {res.GetType().Name}");
            }
            Console.WriteLine("Setting Ready.");
            hasResponse.Set();
        }

        public void AddTaskResponse(string res)
        {
            this.taskResponses.Add(res);
            hasResponse.Set();
        }

        public bool CaptureStdOut(string task_id)
        {
            throw new NotImplementedException();
        }

        public void CompleteJob(string task_id)
        {
        }

        public string GetAgentResponseString()
        {
            return String.Empty;
        }

        public Dictionary<string, ServerJob> GetJobs()
        {
            return new Dictionary<string, ServerJob>();
        }

        public Task<string> GetStdOut()
        {
            throw new NotImplementedException();
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

        public void Write(string? output, string task_id, bool completed, string status)
        {
            Console.WriteLine("Adding 1.");
            taskResponses.Add(
                new TaskResponse
                {
                    user_output = output,
                    completed = completed,
                    status = status,
                    task_id = task_id
                }.ToJson());
            hasResponse.Set();
        }

        public void Write(string? output, string task_id, bool completed)
        {
            this.Write(output, task_id, completed, "");
        }

        public void WriteLine(string? output, string task_id, bool completed, string status)
        {
            taskResponses.Add(
                new TaskResponse
                {
                    user_output = output,
                    completed = completed,
                    status = status,
                    task_id = task_id
                }.ToJson());
            hasResponse.Set();
        }

        public void WriteLine(string? output, string task_id, bool completed)
        {
            WriteLine(output, task_id, completed, "");
        }

        public string GetRecentOutput()
        {
            //Console.WriteLine("We have: " + this.taskResponses.Count());
            return this.taskResponses.Last();
        }
    }
}
