using Agent.Models;

namespace Agent.Interfaces
{
    public interface ITaskManager
    {
        public Task StartTaskAsync(ServerJob job);
        public Task HandleServerResponses(List<ServerResponseResult> responses);
        public Task HandleProxyResponses(string type, List<ServerDatagram> responses);
        public Task HandleDelegateResponses(List<DelegateMessage> responses);
    }
}
