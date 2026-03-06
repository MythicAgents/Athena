using Workflow.Models;

namespace Workflow.Contracts
{
    public interface IForwarderModule : IModule
    {
        public Task ForwardDelegate(DelegateMessage dm);
    }
}
