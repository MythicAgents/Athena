using Agent.Models;

namespace Agent.Interfaces
{
    public interface IProxyPlugin : IPlugin
    {
        public Task HandleDatagram(ServerDatagram sm);
    }

    public interface IBufferedProxyPlugin : IProxyPlugin
    {
        public Task FlushServerMessages();
    }
}
