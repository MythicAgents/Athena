using Workflow.Models;

namespace Workflow.Contracts
{
    public interface IProxyModule : IModule
    {
        public Task HandleDatagram(ServerDatagram sm);
    }

    public interface IBufferedProxyModule : IProxyModule
    {
        public Task FlushServerMessages();
    }
}
