using Agent.Models;

namespace Agent.Interfaces
{
    public interface IForwarderPlugin : IPlugin
    {
        public Task ForwardDelegate(DelegateMessage dm);
    }
}
