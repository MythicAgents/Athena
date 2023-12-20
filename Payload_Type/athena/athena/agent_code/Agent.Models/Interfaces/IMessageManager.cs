using Agent.Models;

namespace Agent.Interfaces
{
    public interface IMessageManager
    {
        public abstract Task AddResponse(string res);
        public abstract Task AddResponse(ResponseResult res);
        public abstract Task AddResponse(FileBrowserResponseResult res);
        public abstract Task AddResponse(ProcessResponseResult res);
        public abstract Task AddResponse(DelegateMessage dm);
        public abstract Task Write(string? output, string task_id, bool completed, string status);
        public abstract Task Write(string? output, string task_id, bool completed);
        public abstract Task WriteLine(string? output, string task_id, bool completed, string status);
        public abstract Task WriteLine(string? output, string task_id, bool completed);
        public abstract Task AddKeystroke(string window_title, string task_id, string key);
        public abstract Task AddResponse(DatagramSource source, ServerDatagram dg);
        //public abstract Task<List<string>> GetTaskResponsesAsync();
        //public abstract Task<List<ServerDatagram>> GetSocksResponsesAsync();
        //public abstract Task<List<ServerDatagram>> GetRpFwdResponsesAsync();
        //public abstract Task<List<DelegateMessage>> GetDelegateResponsesAsync();
        public abstract void AddJob(ServerJob job);
        public abstract Dictionary<string, ServerJob> GetJobs();
        public abstract bool TryGetJob(string task_id, out ServerJob job);
        public abstract void CompleteJob(string task_id);
        public abstract Task<string> GetAgentResponseStringAsync();
        public abstract bool HasResponses();
        public abstract bool CaptureStdOut(string task_id);
        public abstract bool ReleaseStdOut();
        public abstract bool StdIsBusy();
    }
}
