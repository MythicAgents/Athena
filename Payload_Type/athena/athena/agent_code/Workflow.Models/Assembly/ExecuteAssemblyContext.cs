using System.Runtime.Loader;

namespace Workflow.Models
{
    public class ExecuteAssemblyContext : AssemblyLoadContext
    {
        public ExecuteAssemblyContext(string name) : base(name, isCollectible: true)
        {
        }
    }
}
