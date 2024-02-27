using Agent.Models;

namespace Agent.Interfaces
{
    public interface IFilePlugin : IPlugin
    {
        public abstract Task HandleNextMessage(ServerTaskingResponse response);
    }
}
