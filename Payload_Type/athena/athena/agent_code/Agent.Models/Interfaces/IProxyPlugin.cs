using Agent.Models;

namespace Agent.Interfaces
{
    public interface IProxyPlugin : IPlugin
    {
        public Task HandleDatagram(ServerDatagram sm);
    }
}
