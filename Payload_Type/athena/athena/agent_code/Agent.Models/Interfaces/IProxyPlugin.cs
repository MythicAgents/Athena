using Agent.Models;

namespace Agent.Interfaces
{
    public interface IProxyPlugin
    {
        public Task HandleDatagram(ServerDatagram sm);
    }
}
