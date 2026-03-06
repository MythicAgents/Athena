using Workflow.Models;

namespace Workflow.Contracts
{
    public interface IRequestDispatcher
    {
        public Task StartTaskAsync(ServerJob job);
        public Task HandleServerResponses(List<ServerTaskingResponse> responses);
        public Task HandleProxyResponses(string type, List<ServerDatagram> responses);
        public Task HandleDelegateResponses(List<DelegateMessage> responses);
        public Task HandleInteractiveResponses(List<InteractMessage> responses);
    }
}
