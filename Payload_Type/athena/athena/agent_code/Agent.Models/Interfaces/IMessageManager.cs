using Agent.Models;

namespace Agent.Interfaces
{
    public interface IMessageManager
    {
        public abstract Task AddResponse(string res);
        public abstract Task AddResponse(TaskResponse res);
        public abstract Task AddResponse(FileBrowserTaskResponse res);
        public abstract Task AddResponse(ProcessTaskResponse res);
        public abstract Task AddResponse(DelegateMessage dm);
        public abstract Task AddResponse(InteractMessage im);
        public abstract Task Write(string? output, string task_id, bool completed, string status);
        public abstract Task Write(string? output, string task_id, bool completed);
        public abstract Task WriteLine(string? output, string task_id, bool completed, string status);
        public abstract Task WriteLine(string? output, string task_id, bool completed);
        public abstract Task AddKeystroke(string window_title, string task_id, string key);
        public abstract Task AddResponse(DatagramSource source, ServerDatagram dg);
        public abstract void AddJob(ServerJob job);
        public abstract Dictionary<string, ServerJob> GetJobs();
        public abstract bool TryGetJob(string task_id, out ServerJob job);
        public abstract void CompleteJob(string task_id);
        public abstract Task<string> GetAgentResponseStringAsync();
        public abstract bool HasResponses();
        public abstract bool CaptureStdOut(string task_id);
        public abstract bool ReleaseStdOut();
        public abstract bool StdIsBusy();
        public abstract Task<string> GetStdOut();
    }
}
