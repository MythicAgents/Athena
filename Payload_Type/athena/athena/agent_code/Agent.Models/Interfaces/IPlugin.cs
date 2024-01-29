using Agent.Models;

namespace Agent.Interfaces
{
    public interface IPlugin
    {
        public string Name { get; }
        public Task Execute(ServerJob job);
    }
}
