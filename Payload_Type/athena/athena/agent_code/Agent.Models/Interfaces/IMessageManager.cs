using Agent.Models;

namespace Agent.Interfaces
{
    public interface IMessageManager
    {
        void AddTaskResponse(ITaskResponse response);
        void AddTaskResponse(string res);
        void AddTaskResponse(string res, string taskId, bool completed);
        void AddDelegateMessage(DelegateMessage dm);
        void AddInteractMessage(InteractMessage im);
        void AddDatagram(DatagramSource source, ServerDatagram dg);
        bool TryAddDatagram(DatagramSource source, ServerDatagram dg);
        void Write(string? output, string task_id, bool completed, string status);
        void Write(string? output, string task_id, bool completed);
        void WriteLine(string? output, string task_id, bool completed, string status);
        void WriteLine(string? output, string task_id, bool completed);
        void AddKeystroke(string window_title, string task_id, string key);
        void AddJob(ServerJob job);
        Dictionary<string, ServerJob> GetJobs();
        bool TryGetJob(string task_id, out ServerJob job);
        void CompleteJob(string task_id);
        Task<T> DeliverAsync<T>(Func<string, Task<T>> deliver, Func<T, bool> accepted);
        bool HasResponses();
    }
}
