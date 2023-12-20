using Agent.Models;

namespace Agent.Interfaces
{
    public interface IPlugin
    {
        public string Name { get; }
        //public Task Execute(Dictionary<string, string> args);
        public Task Execute(ServerJob job);
    }
}
