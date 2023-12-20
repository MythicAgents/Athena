using Agent.Models;

namespace Agent.Interfaces
{
    public interface IForwarderPlugin
    {
        public Task ForwardDelegate(DelegateMessage dm);
    }
}
