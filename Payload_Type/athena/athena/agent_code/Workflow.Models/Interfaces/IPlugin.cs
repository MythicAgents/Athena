using Workflow.Models;

namespace Workflow.Contracts
{
    public interface IModule
    {
        public string Name { get; }
        public Task Execute(ServerJob job);
    }
}
