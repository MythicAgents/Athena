using Agent.Models;

namespace Agent.Interfaces
{
    public interface IMessageManager
    {
        public abstract void AddTaskResponse(ITaskResponse response);
        public abstract void AddTaskResponse(string res);
        public abstract void AddDelegateMessage(DelegateMessage dm);
        public abstract void AddInteractMessage(InteractMessage im);
        public abstract void AddDatagram(DatagramSource source, ServerDatagram dg);
        public abstract void Write(string? output, string task_id, bool completed, string status);
        public abstract void Write(string? output, string task_id, bool completed);
        public abstract void WriteLine(string? output, string task_id, bool completed, string status);
        public abstract void WriteLine(string? output, string task_id, bool completed);
        public abstract void AddKeystroke(string window_title, string task_id, string key);
        public abstract void AddJob(ServerJob job);
        public abstract Dictionary<string, ServerJob> GetJobs();
        public abstract bool TryGetJob(string task_id, out ServerJob job);
        public abstract void CompleteJob(string task_id);
        public abstract string GetAgentResponseString();
        public abstract bool HasResponses();
        public abstract bool CaptureStdOut(string task_id);
        public abstract bool ReleaseStdOut();
        public abstract bool StdIsBusy();
        public abstract Task<string> GetStdOut();
    }
}
