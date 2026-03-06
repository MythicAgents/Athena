using Workflow.Models;

namespace Workflow.Contracts
{
    public interface IFileModule : IModule
    {
        public abstract Task HandleNextMessage(ServerTaskingResponse response);
    }
}
